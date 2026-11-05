using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>
/// A competition edition (CompetitionSeason): format and rules, participating teams, groups,
/// fixture generation (league / groups / knockout / mixed) and standings.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.ManageCompetitions)]
public class EditionsController(ApplicationDbContext db) : Controller
{
    // ---------------- create / edit ----------------

    [HttpGet]
    public async Task<IActionResult> Create(int competitionId)
    {
        if (!await db.CanManageCompetitionAsync(User, competitionId)) return Forbid();
        var sport = await db.Competitions.Where(c => c.Id == competitionId).Select(c => c.Sport).FirstOrDefaultAsync();
        if (sport == null) return NotFound();
        var model = new EditionFormViewModel
        {
            CompetitionId = competitionId,
            PointsForWin = sport.PointsForWin, PointsForDraw = sport.PointsForDraw, PointsForLoss = sport.PointsForLoss,
            SeasonId = await db.Seasons.Where(s => s.IsCurrent).Select(s => s.Id).FirstOrDefaultAsync(),
            TieBreakers = new CompetitionSeason().GetTieBreakers().Select(c => (TieBreakerCriterion?)c).ToList()
        };
        return View("Edit", await FillAsync(model));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        var e = await db.CompetitionSeasons.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        return View(await FillAsync(new EditionFormViewModel
        {
            Id = e.Id, CompetitionId = e.CompetitionId, SeasonId = e.SeasonId, Format = e.Format, Status = e.Status,
            StartDate = e.StartDate, EndDate = e.EndDate, PointsForWin = e.PointsForWin, PointsForDraw = e.PointsForDraw,
            PointsForLoss = e.PointsForLoss, Regulations = e.Regulations, RowVersion = Convert.ToBase64String(e.RowVersion),
            TieBreakers = e.GetTieBreakers().Select(c => (TieBreakerCriterion?)c).ToList()
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Save(EditionFormViewModel model)
    {
        if (!await db.CanManageCompetitionAsync(User, model.CompetitionId)) return Forbid();
        CompetitionSeason? edition = null;
        if (model.Id != 0)
        {
            edition = await db.CompetitionSeasons.FirstOrDefaultAsync(e => e.Id == model.Id && e.CompetitionId == model.CompetitionId);
            if (edition == null) return NotFound();
            if (edition.Format != model.Format && await db.Matches.AnyAsync(m => m.CompetitionSeasonId == model.Id))
                ModelState.AddModelError(nameof(model.Format), "لا يمكن تغيير نظام البطولة بعد جدولة المباريات.");
        }
        if (!await db.Seasons.AnyAsync(s => s.Id == model.SeasonId)) ModelState.AddModelError(nameof(model.SeasonId), "الموسم غير صالح.");
        if (await db.CompetitionSeasons.AnyAsync(e => e.CompetitionId == model.CompetitionId && e.SeasonId == model.SeasonId && e.Id != model.Id))
            ModelState.AddModelError(nameof(model.SeasonId), "توجد نسخة من البطولة لهذا الموسم.");
        if (model.PointsForWin < model.PointsForDraw || model.PointsForDraw < model.PointsForLoss)
            ModelState.AddModelError(nameof(model.PointsForWin), "يجب أن تكون نقاط الفوز ≥ التعادل ≥ الخسارة.");
        if (model.StartDate > model.EndDate) ModelState.AddModelError(nameof(model.EndDate), "تاريخ النهاية قبل البداية.");
        if (!ModelState.IsValid) return View("Edit", await FillAsync(model));

        edition ??= new CompetitionSeason { CompetitionId = model.CompetitionId };
        if (model.Id == 0) db.CompetitionSeasons.Add(edition);
        else db.SetOriginalRowVersion(edition, model.RowVersion, e => e.RowVersion);

        edition.SeasonId = model.SeasonId;
        edition.Format = model.Format;
        edition.Status = model.Status;
        edition.StartDate = model.StartDate;
        edition.EndDate = model.EndDate;
        edition.PointsForWin = model.PointsForWin;
        edition.PointsForDraw = model.PointsForDraw;
        edition.PointsForLoss = model.PointsForLoss;
        edition.Regulations = model.Regulations?.Trim();
        var criteria = model.TieBreakers.Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();
        edition.TieBreakers = criteria.Count > 0 ? string.Join(",", criteria) : CompetitionSeason.DefaultTieBreakers;

        try
        {
            await db.InTransactionAsync(async () =>
            {
                await db.SaveChangesAsync();
                // Rules may have changed (points, tie-breakers): rebuild the tables.
                if (model.Id != 0) await StandingsCalculator.RecalculateAsync(db, edition.Id);
                // A completed league crowns the table leader.
                if (edition.Status == CompetitionSeasonStatus.Completed && edition.Format == CompetitionFormat.League)
                {
                    var leader = await db.CompetitionStandings.Where(s => s.CompetitionSeasonId == edition.Id && s.Position == 1)
                        .Select(s => (int?)s.TeamId).FirstOrDefaultAsync();
                    if (leader != null) await EditionHelper.SetChampionAsync(db, edition.Id, leader.Value);
                }
                db.AddAudit(HttpContext, model.Id == 0 ? "إضافة نسخة بطولة" : "تعديل نسخة بطولة", nameof(CompetitionSeason), edition.Id, edition.Format.ToString());
                await db.SaveChangesAsync();
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل البيانات من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        TempData["Success"] = "تم حفظ بيانات النسخة.";
        return RedirectToAction(nameof(Manage), new { id = edition.Id });
    }

    // ---------------- management hub ----------------

    [HttpGet]
    public async Task<IActionResult> Manage(int id)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        var e = await db.CompetitionSeasons.AsNoTracking().Include(x => x.Competition).Include(x => x.Season)
            .Include(x => x.Stages).ThenInclude(s => s.Groups).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        var model = new EditionManageViewModel
        {
            Id = e.Id, CompetitionId = e.CompetitionId, CompetitionName = e.Competition.NameAr, CompetitionSlug = e.Competition.Slug,
            SeasonName = e.Season.Name, Format = e.Format, Status = e.Status,
            TieBreakersText = string.Join(" ← ", e.GetTieBreakers().Select(c => c.GetDisplayName())),
            Stages = e.Stages.OrderBy(s => s.SortOrder).Select(s => new StageView
            {
                Id = s.Id, NameAr = s.NameAr, StageType = s.StageType, Legs = s.Legs,
                Groups = s.Groups.OrderBy(g => g.SortOrder).Select(g => new GroupOption(g.Id, g.Name)).ToList()
            }).ToList(),
            Teams = await db.CompetitionTeams.AsNoTracking().Where(ct => ct.CompetitionSeasonId == id)
                .OrderBy(ct => ct.Group != null ? ct.Group.SortOrder : 0).ThenBy(ct => ct.Seed == null).ThenBy(ct => ct.Seed).ThenBy(ct => ct.Team.NameAr)
                .Select(ct => new EditionTeamRow
                {
                    CompetitionTeamId = ct.Id, TeamId = ct.TeamId, NameAr = ct.Team.NameAr, LogoPath = ct.Team.LogoPath,
                    GroupName = ct.Group != null ? ct.Group.Name : null, Seed = ct.Seed,
                    HasMatches = db.Matches.Any(m => m.CompetitionSeasonId == id && (m.HomeTeamId == ct.TeamId || m.AwayTeamId == ct.TeamId))
                }).ToListAsync(),
            Matches = await db.Matches.AsNoTracking().Where(m => m.CompetitionSeasonId == id)
                .OrderBy(m => m.Stage != null ? m.Stage.SortOrder : 0).ThenBy(m => m.RoundNumber).ThenBy(m => m.KickoffAt)
                .ToRows().ToListAsync(),
            Tables = await SportsQueries.LoadStandingsAsync(db, id),
            AvailableTeams = await db.Teams.AsNoTracking()
                .Where(t => t.Status == TeamStatus.Approved && t.SportId == e.Competition.SportId
                    && !db.CompetitionTeams.Any(ct => ct.CompetitionSeasonId == id && ct.TeamId == t.Id))
                .OrderBy(t => t.NameAr).Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync(),
            Venues = await db.Venues.AsNoTracking().OrderBy(v => v.NameAr).Select(v => new SelectListItem(v.NameAr, v.Id.ToString())).ToListAsync()
        };
        return View(model);
    }

    // ---------------- teams & groups ----------------

    [HttpPost]
    public async Task<IActionResult> AddTeams(int id, List<int> teamIds)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        var sportId = await db.CompetitionSeasons.Where(e => e.Id == id).Select(e => e.Competition.SportId).FirstAsync();
        var valid = await db.Teams.Where(t => teamIds.Contains(t.Id) && t.Status == TeamStatus.Approved && t.SportId == sportId
                && !db.CompetitionTeams.Any(ct => ct.CompetitionSeasonId == id && ct.TeamId == t.Id))
            .Select(t => t.Id).ToListAsync();
        foreach (var teamId in valid) db.CompetitionTeams.Add(new CompetitionTeam { CompetitionSeasonId = id, TeamId = teamId });
        if (valid.Count > 0)
        {
            db.AddAudit(HttpContext, "إضافة فرق للبطولة", nameof(CompetitionSeason), id, string.Join(",", valid));
            await db.SaveChangesAsync();
        }
        TempData[valid.Count > 0 ? "Success" : "Error"] = valid.Count > 0 ? $"تمت إضافة {valid.Count} فريق." : "لم يتم اختيار فرق صالحة.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveTeam(int id, int competitionTeamId)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        var ct = await db.CompetitionTeams.FirstOrDefaultAsync(x => x.Id == competitionTeamId && x.CompetitionSeasonId == id);
        if (ct == null) return NotFound();
        if (await db.Matches.AnyAsync(m => m.CompetitionSeasonId == id && (m.HomeTeamId == ct.TeamId || m.AwayTeamId == ct.TeamId)))
        {
            TempData["Error"] = "لا يمكن إزالة فريق له مباريات في هذه النسخة.";
            return RedirectToAction(nameof(Manage), new { id });
        }
        db.CompetitionTeams.Remove(ct);
        db.AddAudit(HttpContext, "إزالة فريق من البطولة", nameof(CompetitionSeason), id, ct.TeamId.ToString());
        await db.SaveChangesAsync();
        TempData["Success"] = "تمت إزالة الفريق.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetSeed(int id, int competitionTeamId, int? seed)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        int? value = seed > 0 ? seed : null; // computed outside the expression tree
        await db.CompetitionTeams.Where(x => x.Id == competitionTeamId && x.CompetitionSeasonId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Seed, value));
        return RedirectToAction(nameof(Manage), new { id });
    }

    /// <summary>Creates the group stage with N groups and distributes the teams (seeded "snake" order, or random).</summary>
    [HttpPost]
    public async Task<IActionResult> SetupGroups(int id, int groupCount, int qualifiersPerGroup, bool shuffle)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        if (!ModelState.IsValid) return Back(id, "قيم غير صالحة.");
        var edition = await db.CompetitionSeasons.FirstAsync(e => e.Id == id);
        if (edition.Format is not (CompetitionFormat.Groups or CompetitionFormat.Mixed))
            return Back(id, "نظام البطولة لا يتضمن مجموعات.");
        var teams = await db.CompetitionTeams.Where(ct => ct.CompetitionSeasonId == id && !ct.IsWithdrawn)
            .Include(ct => ct.Team).ToListAsync();
        if (groupCount < 1 || groupCount > 16 || teams.Count < groupCount * 2) return Back(id, "عدد المجموعات غير مناسب لعدد الفرق (فريقان على الأقل لكل مجموعة).");
        if (qualifiersPerGroup < 1 || qualifiersPerGroup > teams.Count / groupCount) return Back(id, "عدد المتأهلين من كل مجموعة غير صالح.");

        var stage = await EditionHelper.GetOrCreateStageAsync(db, id, StageType.Group);
        if (await db.Matches.AnyAsync(m => m.StageId == stage.Id)) return Back(id, "تمت جدولة مباريات المجموعات. احذفها أولاً لإعادة توزيع الفرق.");

        await db.InTransactionAsync(async () =>
        {
            foreach (var ct in teams) ct.GroupId = null;
            await db.SaveChangesAsync();
            // Group tables reference the groups (FK RESTRICT): drop them before the groups; they are rebuilt below.
            await db.CompetitionStandings.Where(s => s.CompetitionSeasonId == id && s.GroupId != null).ExecuteDeleteAsync();
            db.CompetitionGroups.RemoveRange(stage.Groups);
            await db.SaveChangesAsync();

            stage.QualifiersPerGroup = qualifiersPerGroup;
            var groups = Enumerable.Range(0, groupCount)
                .Select(i => new CompetitionGroup { StageId = stage.Id, Name = ((char)('A' + i)).ToString(), SortOrder = i + 1 }).ToList();
            db.CompetitionGroups.AddRange(groups);
            await db.SaveChangesAsync();

            var ordered = shuffle
                ? teams.OrderBy(_ => Random.Shared.Next()).ToList()
                : teams.OrderBy(t => t.Seed == null).ThenBy(t => t.Seed).ThenBy(t => t.Team.NameAr).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                // Snake: A B C D D C B A ... keeps seeded teams apart.
                var row = i / groupCount;
                var col = i % groupCount;
                ordered[i].GroupId = groups[row % 2 == 0 ? col : groupCount - 1 - col].Id;
            }
            db.AddAudit(HttpContext, "توزيع الفرق على المجموعات", nameof(CompetitionSeason), id, $"{groupCount} مجموعات");
            await db.SaveChangesAsync();
            await StandingsCalculator.RecalculateAsync(db, id);
        });
        TempData["Success"] = "تم إنشاء المجموعات وتوزيع الفرق.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> MoveTeam(int id, int competitionTeamId, int groupId)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        var ct = await db.CompetitionTeams.FirstOrDefaultAsync(x => x.Id == competitionTeamId && x.CompetitionSeasonId == id);
        var group = await db.CompetitionGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.Stage.CompetitionSeasonId == id);
        if (ct == null || group == null) return NotFound();
        if (await db.Matches.AnyAsync(m => m.StageId == group.StageId)) return Back(id, "تمت جدولة مباريات المجموعات بالفعل.");
        ct.GroupId = group.Id;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Manage), new { id });
    }

    // ---------------- fixtures ----------------

    /// <summary>
    /// Generates the season's first fixtures: the full league, every group's round-robin, or the first knockout round.
    /// Kick-off is entered in Muscat time; each round is <paramref name="daysBetweenRounds"/> days after the previous.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GenerateFixtures(int id, DateOnly startDate, TimeOnly kickoffTime, int daysBetweenRounds, int legs, int? venueId)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        if (!ModelState.IsValid || daysBetweenRounds is < 1 or > 60 || legs is < 1 or > 2) return Back(id, "إعدادات الجدولة غير صالحة.");
        var edition = await db.CompetitionSeasons.Include(e => e.Competition).FirstAsync(e => e.Id == id);
        var firstKickoff = startDate.ToDateTime(kickoffTime).FromMuscat();
        var homeVenues = await db.CompetitionTeams.Where(ct => ct.CompetitionSeasonId == id)
            .Select(ct => new { ct.TeamId, ct.Team.HomeVenueId }).ToDictionaryAsync(x => x.TeamId, x => x.HomeVenueId);

        string? error = null;
        var created = 0;
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            switch (edition.Format)
            {
                case CompetitionFormat.League:
                {
                    var stage = await EditionHelper.GetOrCreateStageAsync(db, id, StageType.League, legs);
                    if (await db.Matches.AnyAsync(m => m.StageId == stage.Id)) { error = "تمت جدولة مباريات الدوري مسبقاً."; return; }
                    var teams = await db.CompetitionTeams.Where(ct => ct.CompetitionSeasonId == id && !ct.IsWithdrawn)
                        .OrderBy(ct => ct.Team.NameAr).Select(ct => ct.TeamId).ToListAsync();
                    if (teams.Count < 2) { error = "أضف فريقين على الأقل."; return; }
                    stage.Legs = legs;
                    created = EditionHelper.AddRoundRobinMatches(db, id, edition.Competition.SportId, stage, null, teams, legs,
                        firstKickoff, daysBetweenRounds, venueId, homeVenues);
                    break;
                }
                case CompetitionFormat.Groups:
                case CompetitionFormat.Mixed:
                {
                    var stage = await db.CompetitionStages.Include(s => s.Groups)
                        .FirstOrDefaultAsync(s => s.CompetitionSeasonId == id && s.StageType == StageType.Group);
                    if (stage == null || stage.Groups.Count == 0) { error = "أنشئ المجموعات أولاً."; return; }
                    if (await db.Matches.AnyAsync(m => m.StageId == stage.Id)) { error = "تمت جدولة مباريات المجموعات مسبقاً."; return; }
                    stage.Legs = legs;
                    foreach (var group in stage.Groups)
                    {
                        var teams = await db.CompetitionTeams.Where(ct => ct.GroupId == group.Id && !ct.IsWithdrawn)
                            .OrderBy(ct => ct.Team.NameAr).Select(ct => ct.TeamId).ToListAsync();
                        created += EditionHelper.AddRoundRobinMatches(db, id, edition.Competition.SportId, stage, group.Id, teams, legs,
                            firstKickoff, daysBetweenRounds, venueId, homeVenues);
                    }
                    break;
                }
                case CompetitionFormat.Knockout:
                {
                    var tracked = await db.CompetitionSeasons.Include(e => e.Competition).FirstAsync(e => e.Id == id);
                    (created, error) = await EditionHelper.GenerateNextKnockoutRoundAsync(db, tracked, firstKickoff, venueId);
                    if (error == null && created == 0) error = "لا توجد مباريات لإنشائها.";
                    if (error != null) return;
                    break;
                }
            }
            if (edition.Status is CompetitionSeasonStatus.Draft or CompetitionSeasonStatus.Registration)
                await db.CompetitionSeasons.Where(e => e.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, CompetitionSeasonStatus.InProgress));
            db.AddAudit(HttpContext, "جدولة مباريات", nameof(CompetitionSeason), id, $"{created} مباراة");
            await db.SaveChangesAsync();
            await StandingsCalculator.RecalculateAsync(db, id);
        });

        if (error != null) return Back(id, error);
        TempData["Success"] = $"تمت جدولة {created} مباراة.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    /// <summary>Knockout progression: first knockout round after the groups (mixed), or the next round.</summary>
    [HttpPost]
    public async Task<IActionResult> NextKnockoutRound(int id, DateOnly date, TimeOnly kickoffTime, int? venueId)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        if (!ModelState.IsValid) return Back(id, "التاريخ أو الوقت غير صالح.");
        string? error = null;
        var created = 0;
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var edition = await db.CompetitionSeasons.Include(e => e.Competition).FirstAsync(e => e.Id == id);
            if (edition.Format is not (CompetitionFormat.Knockout or CompetitionFormat.Mixed)) { error = "نظام البطولة لا يتضمن أدواراً إقصائية."; return; }
            (created, error) = await EditionHelper.GenerateNextKnockoutRoundAsync(db, edition, date.ToDateTime(kickoffTime).FromMuscat(), venueId);
            if (error != null) return;
            db.AddAudit(HttpContext, "جدولة دور إقصائي", nameof(CompetitionSeason), id, $"{created} مباراة");
            await db.SaveChangesAsync();
        });
        if (error != null) return Back(id, error);
        TempData["Success"] = $"تمت جدولة {created} مباراة في الدور التالي.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    /// <summary>Removes all not-yet-played matches of a stage so the fixtures can be regenerated.</summary>
    [HttpPost]
    public async Task<IActionResult> ClearStageFixtures(int id, int stageId)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        if (await db.Matches.AnyAsync(m => m.StageId == stageId && m.CompetitionSeasonId == id && m.Status != MatchStatus.Scheduled && m.Status != MatchStatus.Postponed))
            return Back(id, "لا يمكن حذف الجدول بعد بدء مباريات في هذه المرحلة.");
        var deleted = 0;
        await db.InTransactionAsync(async () =>
        {
            var ids = await db.Matches.Where(m => m.StageId == stageId && m.CompetitionSeasonId == id).Select(m => m.Id).ToListAsync();
            await db.MatchOfficials.Where(o => ids.Contains(o.MatchId)).ExecuteDeleteAsync();
            await db.UserFollows.Where(f => f.MatchId != null && ids.Contains(f.MatchId.Value)).ExecuteDeleteAsync();
            deleted = await db.Matches.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync();
            db.AddAudit(HttpContext, "حذف جدول مرحلة", nameof(CompetitionSeason), id, $"{deleted} مباراة");
            await db.SaveChangesAsync();
            await StandingsCalculator.RecalculateAsync(db, id);
        });
        TempData["Success"] = $"تم حذف {deleted} مباراة غير ملعوبة.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    // ---------------- standings ----------------

    [HttpPost]
    public async Task<IActionResult> Recalculate(int id)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        await db.InTransactionAsync(() => StandingsCalculator.RecalculateAsync(db, id));
        TempData["Success"] = "تم تحديث الترتيب والإحصائيات.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    /// <summary>Organiser's point deduction / bonus for a team (kept across recalculations).</summary>
    [HttpPost]
    public async Task<IActionResult> AdjustPoints(int id, int teamId, int adjustment, string? reason)
    {
        if (!await db.CanManageEditionAsync(User, id)) return Forbid();
        if (!ModelState.IsValid || adjustment is < -100 or > 100) return Back(id, "قيمة التعديل غير صالحة.");
        await db.InTransactionAsync(async () =>
        {
            await db.CompetitionStandings.Where(s => s.CompetitionSeasonId == id && s.TeamId == teamId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PointsAdjustment, adjustment));
            db.AddAudit(HttpContext, "تعديل نقاط فريق", nameof(CompetitionSeason), id, $"فريق {teamId}: {adjustment} — {reason}");
            await db.SaveChangesAsync();
            await StandingsCalculator.RecalculateAsync(db, id);
        });
        TempData["Success"] = "تم تعديل النقاط وتحديث الترتيب.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    private IActionResult Back(int id, string error)
    {
        TempData["Error"] = error;
        return RedirectToAction(nameof(Manage), new { id });
    }

    private async Task<EditionFormViewModel> FillAsync(EditionFormViewModel model)
    {
        model.CompetitionName = await db.Competitions.Where(c => c.Id == model.CompetitionId).Select(c => c.NameAr).FirstOrDefaultAsync();
        model.Seasons = await db.Seasons.AsNoTracking().OrderByDescending(s => s.StartDate)
            .Select(s => new SelectListItem(s.Name + (s.IsCurrent ? " (الحالي)" : ""), s.Id.ToString())).ToListAsync();
        while (model.TieBreakers.Count < 5) model.TieBreakers.Add(null);
        return model;
    }
}
