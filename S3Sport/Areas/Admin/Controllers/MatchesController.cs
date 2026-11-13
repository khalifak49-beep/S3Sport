using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Hubs;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>
/// Match scheduling: date/time, venue, officials, assigned operator, postponement / cancellation,
/// and manual result entry for matches without live coverage. Live play is handled by the operator screen.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.ManageCompetitions)]
public class MatchesController(ApplicationDbContext db, IHubContext<LiveHub> hub) : Controller
{
    private static readonly MatchStatus[] EditableStatuses = [MatchStatus.Scheduled, MatchStatus.Postponed, MatchStatus.Cancelled, MatchStatus.Finished];

    [HttpGet]
    public async Task<IActionResult> Index(int? editionId, MatchStatus? status, DateOnly? from, DateOnly? to, int page = 1)
    {
        var query = db.ManageableMatches(User).AsNoTracking();
        if (editionId.HasValue) query = query.Where(m => m.CompetitionSeasonId == editionId);
        if (status.HasValue) query = query.Where(m => m.Status == status);
        if (from.HasValue) { var f = from.Value.ToDateTime(TimeOnly.MinValue).FromMuscat(); query = query.Where(m => m.KickoffAt >= f); }
        if (to.HasValue) { var t = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).FromMuscat(); query = query.Where(m => m.KickoffAt < t); }

        return View(new MatchListViewModel
        {
            Results = await PagedResult<MatchRow>.CreateAsync(query.OrderByDescending(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime)
                .ThenBy(m => m.KickoffAt).ToRows(), page, 30),
            EditionId = editionId, Status = status, From = from, To = to,
            Editions = await EditionOptionsAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? editionId)
    {
        if (editionId.HasValue && !await db.CanManageEditionAsync(User, editionId.Value)) return Forbid();
        if (!editionId.HasValue && !User.IsAdmin()) return Forbid(); // friendlies are admin-only
        var model = new MatchFormViewModel
        {
            CompetitionSeasonId = editionId,
            KickoffLocal = DateTime.UtcNow.ToMuscat().Date.AddDays(1).AddHours(17)
        };
        return View("Edit", await FillAsync(model));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var m = await db.ManageableMatches(User).AsNoTracking().Include(x => x.Officials).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();
        var model = new MatchFormViewModel
        {
            Id = m.Id, CompetitionSeasonId = m.CompetitionSeasonId, StageId = m.StageId, GroupId = m.GroupId,
            RoundNumber = m.RoundNumber, RoundName = m.RoundName, HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId,
            KickoffLocal = m.KickoffAt.ToMuscat(), VenueId = m.VenueId, Status = m.Status, OperatorUserId = m.OperatorUserId, Notes = m.Notes,
            IsTournament = m.TournamentName != null, TournamentName = m.TournamentName,
            RefereeName = m.Officials.FirstOrDefault(o => o.Role == MatchOfficialRole.Referee)?.Name,
            Assistant1Name = m.Officials.FirstOrDefault(o => o.Role == MatchOfficialRole.AssistantReferee1)?.Name,
            Assistant2Name = m.Officials.FirstOrDefault(o => o.Role == MatchOfficialRole.AssistantReferee2)?.Name,
            ResultHome = m.HomeScore, ResultAway = m.AwayScore, PenaltyHome = m.HomePenaltyScore, PenaltyAway = m.AwayPenaltyScore,
            HasStarted = m.Status is MatchStatus.Live or MatchStatus.HalfTime || await db.MatchEvents.AnyAsync(e => e.MatchId == id),
            RowVersion = Convert.ToBase64String(m.RowVersion)
        };
        return View(await FillAsync(model));
    }

    [HttpPost]
    public async Task<IActionResult> Save(MatchFormViewModel model)
    {
        Match? match = null;
        if (model.Id != 0)
        {
            match = await db.ManageableMatches(User).Include(m => m.Officials).FirstOrDefaultAsync(m => m.Id == model.Id);
            if (match == null) return NotFound();
            if (match.Status is MatchStatus.Live or MatchStatus.HalfTime)
            {
                TempData["Error"] = "المباراة جارية الآن؛ تُدار من شاشة مسؤول المباراة.";
                return RedirectToAction(nameof(Edit), new { id = model.Id });
            }
        }
        // A league edition has a single stage: matches saved without one belong to it.
        if (model.CompetitionSeasonId != null && model.StageId == null)
            model.StageId = await db.CompetitionStages
                .Where(s => s.CompetitionSeasonId == model.CompetitionSeasonId && s.StageType == StageType.League)
                .Select(s => (int?)s.Id).FirstOrDefaultAsync();

        await ValidateAsync(model, match);
        // A match outside the platform's competitions may carry a typed tournament name (optional; friendly otherwise).
        var tournament = model.CompetitionSeasonId == null && model.IsTournament ? model.TournamentName?.Trim() : null;
        if (string.IsNullOrEmpty(tournament)) tournament = null;
        if (model.CompetitionSeasonId == null && model.IsTournament && tournament == null)
            ModelState.AddModelError(nameof(model.TournamentName), "اكتب اسم البطولة، أو اختر «ودية».");
        if (!ModelState.IsValid) return View("Edit", await FillAsync(model));

        var isNew = match == null;
        var oldKickoff = match?.KickoffAt;
        var oldStatus = match?.Status;
        var oldEdition = match?.CompetitionSeasonId;
        var hasEvents = !isNew && await db.MatchEvents.AnyAsync(e => e.MatchId == model.Id && !e.IsCancelled);

        if (isNew)
        {
            match = new Match { SportId = SeedData.FootballSportId };
            db.Matches.Add(match);
        }
        else db.SetOriginalRowVersion(match!, model.RowVersion, m => m.RowVersion);

        match!.CompetitionSeasonId = model.CompetitionSeasonId;
        match.StageId = model.StageId;
        match.GroupId = model.GroupId;
        match.RoundNumber = model.RoundNumber;
        match.RoundName = string.IsNullOrWhiteSpace(model.RoundName) ? (model.RoundNumber != null ? $"الجولة {model.RoundNumber}" : null) : model.RoundName.Trim();
        match.HomeTeamId = model.HomeTeamId;
        match.AwayTeamId = model.AwayTeamId;
        match.KickoffAt = model.KickoffLocal!.Value.FromMuscat();
        match.VenueId = model.VenueId;
        match.OperatorUserId = string.IsNullOrEmpty(model.OperatorUserId) ? null : model.OperatorUserId;
        match.Notes = model.Notes?.Trim();
        match.TournamentName = tournament;
        match.Status = model.Status;

        if (model.Status == MatchStatus.Finished && !hasEvents)
        {
            // Manual result for a match that was not covered live.
            match.HomeScore = model.ResultHome!.Value;
            match.AwayScore = model.ResultAway!.Value;
            match.HomePenaltyScore = model.PenaltyHome;
            match.AwayPenaltyScore = model.PenaltyAway;
            match.FinishedAt ??= DateTime.UtcNow;
            match.WinnerTeamId = WinnerOf(match);
        }
        else if (model.Status != MatchStatus.Finished && !hasEvents)
        {
            match.HomeScore = match.AwayScore = 0;
            match.HomePenaltyScore = match.AwayPenaltyScore = null;
            match.WinnerTeamId = null;
            match.FinishedAt = null;
        }

        SetOfficial(match, MatchOfficialRole.Referee, model.RefereeName);
        SetOfficial(match, MatchOfficialRole.AssistantReferee1, model.Assistant1Name);
        SetOfficial(match, MatchOfficialRole.AssistantReferee2, model.Assistant2Name);

        try
        {
            await db.InTransactionAsync(async () =>
            {
                await db.SaveChangesAsync();
                db.AddAudit(HttpContext, isNew ? "جدولة مباراة" : "تعديل مباراة", nameof(Match), match.Id,
                    $"{model.Status.GetDisplayName()} — {match.KickoffAt.ToMuscatString()}");
                await db.SaveChangesAsync();
                foreach (var edition in new[] { oldEdition, match.CompetitionSeasonId }.Where(e => e != null).Distinct())
                    await StandingsCalculator.RecalculateAsync(db, edition!.Value);
                if (match.Status == MatchStatus.Finished) await EditionHelper.CheckKnockoutChampionAsync(db, match.Id);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل المباراة من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        // Notify followers after the commit.
        if (!isNew)
        {
            var title = await MatchTitleAsync(match.Id);
            if (oldKickoff != match.KickoffAt && match.Status == MatchStatus.Scheduled)
                await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id, NotificationType.MatchRescheduled,
                    $"تغيير موعد: {title}", $"الموعد الجديد: {match.KickoffAt.ToMuscatString("dddd yyyy/MM/dd HH:mm")}");
            if (oldStatus != match.Status && match.Status is MatchStatus.Postponed or MatchStatus.Cancelled)
                await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id, NotificationType.MatchRescheduled,
                    $"{(match.Status == MatchStatus.Postponed ? "تأجيل" : "إلغاء")} مباراة: {title}", match.Notes);
            if (oldStatus != match.Status && match.Status == MatchStatus.Finished)
                await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id, NotificationType.MatchEnded,
                    $"النتيجة النهائية: {title}", $"{match.HomeScore} - {match.AwayScore}");
        }
        await hub.Clients.Groups(LiveHub.MatchGroup(match.Id), LiveHub.LiveGroup).SendAsync("matchChanged", new { matchId = match.Id });

        TempData["Success"] = isNew ? "تمت جدولة المباراة." : "تم حفظ المباراة.";
        return model.CompetitionSeasonId != null
            ? RedirectToAction("Manage", "Editions", new { id = model.CompetitionSeasonId })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var match = await db.ManageableMatches(User).FirstOrDefaultAsync(m => m.Id == id);
        if (match == null) return NotFound();
        if (match.Status is not (MatchStatus.Scheduled or MatchStatus.Postponed or MatchStatus.Cancelled) || await db.MatchEvents.AnyAsync(e => e.MatchId == id))
        {
            TempData["Error"] = "لا يمكن حذف مباراة لُعبت أو سُجلت لها أحداث. يمكنك إلغاؤها.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var edition = match.CompetitionSeasonId;
        await db.InTransactionAsync(async () =>
        {
            await db.MatchOfficials.Where(o => o.MatchId == id).ExecuteDeleteAsync();
            await db.UserFollows.Where(f => f.MatchId == id).ExecuteDeleteAsync();
            db.Matches.Remove(match); // soft delete
            db.AddAudit(HttpContext, "حذف مباراة", nameof(Match), id, null);
            await db.SaveChangesAsync();
        });
        TempData["Success"] = "تم حذف المباراة.";
        return edition != null ? RedirectToAction("Manage", "Editions", new { id = edition }) : RedirectToAction(nameof(Index));
    }

    // ---------- helpers ----------

    private async Task ValidateAsync(MatchFormViewModel model, Match? existing)
    {
        if (model.HomeTeamId == model.AwayTeamId) ModelState.AddModelError(nameof(model.AwayTeamId), "لا يمكن أن يلعب الفريق ضد نفسه.");
        if (!EditableStatuses.Contains(model.Status)) ModelState.AddModelError(nameof(model.Status), "الحالات المباشرة تُدار من شاشة مسؤول المباراة.");

        if (model.CompetitionSeasonId is { } editionId)
        {
            if (!await db.CanManageEditionAsync(User, editionId)) ModelState.AddModelError(nameof(model.CompetitionSeasonId), "لا تملك صلاحية على هذه البطولة.");
            var participants = await db.CompetitionTeams.Where(ct => ct.CompetitionSeasonId == editionId).Select(ct => ct.TeamId).ToListAsync();
            if (!participants.Contains(model.HomeTeamId) || !participants.Contains(model.AwayTeamId))
                ModelState.AddModelError(nameof(model.HomeTeamId), "الفريقان يجب أن يكونا مسجلين في هذه البطولة.");
            if (model.StageId != null && !await db.CompetitionStages.AnyAsync(s => s.Id == model.StageId && s.CompetitionSeasonId == editionId))
                ModelState.AddModelError(nameof(model.StageId), "المرحلة لا تتبع هذه البطولة.");
            if (model.GroupId != null && !await db.CompetitionGroups.AnyAsync(g => g.Id == model.GroupId && g.StageId == model.StageId))
                ModelState.AddModelError(nameof(model.GroupId), "المجموعة لا تتبع المرحلة المختارة.");
        }
        else
        {
            if (!User.IsAdmin()) ModelState.AddModelError(nameof(model.CompetitionSeasonId), "اختر البطولة.");
            model.StageId = model.GroupId = null;
            // A guest opponent (added by a team manager) stays valid for the friendly it already belongs to.
            int[] current = existing != null ? [existing.HomeTeamId, existing.AwayTeamId] : [];
            bool Valid(Team t) => t.Status == TeamStatus.Approved || (t.Status == TeamStatus.Guest && current.Contains(t.Id));
            var chosen = await db.Teams.AsNoTracking().Where(t => t.Id == model.HomeTeamId || t.Id == model.AwayTeamId).ToListAsync();
            if (!chosen.Any(t => t.Id == model.HomeTeamId && Valid(t)) || !chosen.Any(t => t.Id == model.AwayTeamId && Valid(t)))
                ModelState.AddModelError(nameof(model.HomeTeamId), "اختر فريقين معتمدين.");
            if (existing?.OrganizerTeamId is { } organizer && organizer != model.HomeTeamId && organizer != model.AwayTeamId)
                ModelState.AddModelError(nameof(model.HomeTeamId), "هذه المباراة نظّمها فريق؛ يجب أن يبقى أحد طرفيها.");
        }

        if (model.VenueId != null && !await db.Venues.AnyAsync(v => v.Id == model.VenueId)) ModelState.AddModelError(nameof(model.VenueId), "الملعب غير صالح.");
        if (!string.IsNullOrEmpty(model.OperatorUserId) && !await CanBeOperatorAsync(model.OperatorUserId, model.HomeTeamId, model.AwayTeamId))
            ModelState.AddModelError(nameof(model.OperatorUserId), "المستخدم المختار لا يملك صلاحية تسجيل الأحداث لهذه المباراة (مسؤول الفريق يسجّل مباريات فريقه فقط).");

        var hasEvents = existing != null && await db.MatchEvents.AnyAsync(e => e.MatchId == existing.Id && !e.IsCancelled);
        if (hasEvents && (existing!.HomeTeamId != model.HomeTeamId || existing.AwayTeamId != model.AwayTeamId))
            ModelState.AddModelError(nameof(model.HomeTeamId), "لا يمكن تغيير الفريقين بعد تسجيل أحداث للمباراة.");

        if (model.Status == MatchStatus.Finished && !hasEvents)
        {
            if (model.ResultHome is null or < 0 || model.ResultAway is null or < 0)
                ModelState.AddModelError(nameof(model.ResultHome), "أدخل النتيجة النهائية.");
            var isKnockout = model.StageId != null && await db.CompetitionStages.AnyAsync(s => s.Id == model.StageId && s.StageType == StageType.Knockout);
            if (isKnockout && model.ResultHome == model.ResultAway &&
                (model.PenaltyHome is null || model.PenaltyAway is null || model.PenaltyHome == model.PenaltyAway))
                ModelState.AddModelError(nameof(model.PenaltyHome), "مباراة إقصائية متعادلة: أدخل نتيجة ركلات الترجيح (بدون تعادل).");
            if (!isKnockout) model.PenaltyHome = model.PenaltyAway = null;
        }
    }

    private static int? WinnerOf(Match m)
    {
        if (m.HomeScore != m.AwayScore) return m.HomeScore > m.AwayScore ? m.HomeTeamId : m.AwayTeamId;
        if (m.HomePenaltyScore != null && m.AwayPenaltyScore != null && m.HomePenaltyScore != m.AwayPenaltyScore)
            return m.HomePenaltyScore > m.AwayPenaltyScore ? m.HomeTeamId : m.AwayTeamId;
        return null;
    }

    private void SetOfficial(Match match, MatchOfficialRole role, string? name)
    {
        var existing = match.Officials.FirstOrDefault(o => o.Role == role);
        if (string.IsNullOrWhiteSpace(name))
        {
            // Delete the row itself: detaching it from the required relationship alone throws.
            if (existing != null) db.MatchOfficials.Remove(existing);
        }
        else if (existing == null) match.Officials.Add(new MatchOfficial { Role = role, Name = name.Trim() });
        else existing.Name = name.Trim();
    }

    private Task<string> MatchTitleAsync(int id) =>
        db.Matches.Where(m => m.Id == id).Select(m => m.HomeTeam.NameAr + " × " + m.AwayTeam.NameAr).FirstAsync();

    private async Task<List<SelectListItem>> EditionOptionsAsync() =>
        await db.CompetitionSeasons.AsNoTracking()
            .Where(e => db.ManageableCompetitions(User).Any(c => c.Id == e.CompetitionId))
            .OrderByDescending(e => e.Season.StartDate).ThenBy(e => e.Competition.NameAr)
            .Select(e => new SelectListItem(e.Competition.NameAr + " — " + e.Season.Name, e.Id.ToString())).ToListAsync();

    /// <summary>Users who may record live events (operators, tournament managers, admins).</summary>
    private static readonly string[] OperatorRoles = [AppRoles.MatchOperator, AppRoles.TournamentManager, AppRoles.Admin, AppRoles.SuperAdmin];

    private IQueryable<ApplicationUser> UsersInRoles(params string[] roles) => db.Users.Where(u => u.IsActive
        && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && roles.Contains(r.Name!))));

    /// <summary>Active managers (TeamManager role) of approved teams: the account that registered the team, plus any added later.</summary>
    private IQueryable<TeamMember> TeamManagers() => db.TeamMembers.Where(x => x.IsActive && x.Role == TeamMemberRole.Manager
        && x.Team.Status == TeamStatus.Approved && UsersInRoles(AppRoles.TeamManager).Any(u => u.Id == x.UserId));

    /// <summary>Platform operators, then each team's manager labelled with the team, so the admin can hand a match to the team's owner.</summary>
    private async Task<List<SelectListItem>> OperatorOptionsAsync()
    {
        var staff = await UsersInRoles(OperatorRoles).AsNoTracking().OrderBy(u => u.FullName)
            .Select(u => new SelectListItem(u.FullName + " (" + u.Email + ")", u.Id)).ToListAsync();
        var managers = await TeamManagers().AsNoTracking().OrderBy(x => x.Team.NameAr).ThenBy(x => x.User.FullName)
            .Select(x => new { x.UserId, Label = "مسؤول فريق " + x.Team.NameAr + " — " + x.User.FullName + " (" + x.User.Email + ")" }).ToListAsync();
        var staffIds = staff.Select(s => s.Value).ToHashSet();
        return [.. staff, .. managers.Where(m => !staffIds.Contains(m.UserId)).Select(m => new SelectListItem(m.Label, m.UserId))];
    }

    /// <summary>Platform operators may run any match; a team manager only matches of a team they manage.</summary>
    private async Task<bool> CanBeOperatorAsync(string userId, int homeTeamId, int awayTeamId) =>
        await UsersInRoles(OperatorRoles).AnyAsync(u => u.Id == userId)
        || await TeamManagers().AnyAsync(x => x.UserId == userId && (x.TeamId == homeTeamId || x.TeamId == awayTeamId));

    private async Task<MatchFormViewModel> FillAsync(MatchFormViewModel model)
    {
        model.Editions = await EditionOptionsAsync();
        if (model.CompetitionSeasonId is { } editionId)
        {
            model.Teams = await db.CompetitionTeams.AsNoTracking().Where(ct => ct.CompetitionSeasonId == editionId).OrderBy(ct => ct.Team.NameAr)
                .Select(ct => new SelectListItem(ct.Team.NameAr, ct.TeamId.ToString())).ToListAsync();
            model.Stages = await db.CompetitionStages.AsNoTracking().Where(s => s.CompetitionSeasonId == editionId).OrderBy(s => s.SortOrder)
                .Select(s => new SelectListItem(s.NameAr, s.Id.ToString())).ToListAsync();
            model.Groups = await db.CompetitionGroups.AsNoTracking().Where(g => g.Stage.CompetitionSeasonId == editionId).OrderBy(g => g.SortOrder)
                .Select(g => new SelectListItem("المجموعة " + g.Name, g.Id.ToString())).ToListAsync();
        }
        else
        {
            model.Teams = await db.Teams.AsNoTracking()
                .Where(t => t.Status == TeamStatus.Approved || (t.Status == TeamStatus.Guest && (t.Id == model.HomeTeamId || t.Id == model.AwayTeamId)))
                .OrderBy(t => t.NameAr)
                .Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync();
        }
        model.Venues = await db.Venues.AsNoTracking().OrderBy(v => v.NameAr).Select(v => new SelectListItem(v.NameAr, v.Id.ToString())).ToListAsync();
        model.Operators = await OperatorOptionsAsync();
        return model;
    }
}
