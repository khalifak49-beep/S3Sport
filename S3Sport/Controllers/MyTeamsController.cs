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

namespace S3Sport.Controllers;

/// <summary>
/// Team-manager self-service: team info, squad and the team's friendlies. Every action verifies the user manages that team,
/// so a TeamManager can never touch another team. Moves between two teams are recorded by admins (Players → transfer).
/// Competition matches stay with the tournament managers; a team only organises (and records live) its own friendlies.
/// </summary>
[Authorize(Policy = AppPolicies.ManageTeams)]
public class MyTeamsController(ApplicationDbContext db, IWebHostEnvironment env, IHubContext<LiveHub> hub, ILogger<MyTeamsController> logger) : Controller
{
    private static readonly MatchStatus[] FriendlyStatuses = [MatchStatus.Scheduled, MatchStatus.Postponed, MatchStatus.Cancelled];

    private string UserId => User.GetUserId()!;

    public async Task<IActionResult> Index()
    {
        // Teams this user is a member of (admins manage all teams from the admin panel instead).
        var list = await db.Teams.AsNoTracking().Where(t => t.Members.Any(m => m.UserId == UserId && m.IsActive))
            .OrderBy(t => t.NameAr).ToCards(db, UserId).ToListAsync();
        if (list.Count == 1) return RedirectToAction(nameof(Squad), new { id = list[0].Id });
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Squad(int id, string? search)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        var model = new MyTeamViewModel
        {
            TeamId = team.Id, NameAr = team.NameAr, Slug = team.Slug, LogoPath = team.LogoPath, Status = team.Status,
            Squad = await db.Players.AsNoTracking().Where(p => p.TeamPlayers.Any(tp => tp.IsActive && tp.TeamId == id))
                .ToListItems(db).OrderBy(p => p.ShirtNumber ?? 999).ThenBy(p => p.FullNameAr).ToListAsync(),
            FreeAgentSearch = search
        };
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length >= 2)
        {
            var s = search.Trim();
            model.FreeAgents = await db.Players.AsNoTracking()
                .Where(p => !p.TeamPlayers.Any(tp => tp.IsActive) && (p.FullNameAr.Contains(s) || (p.FullNameEn != null && p.FullNameEn.Contains(s))))
                .OrderBy(p => p.FullNameAr).Take(20).ToListItems(db).ToListAsync();
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Info(int id)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var t = await db.Teams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        return View(new MyTeamInfoViewModel
        {
            TeamId = t.Id, NameAr = t.NameAr, NameEn = t.NameEn, CurrentName = t.NameAr, ShortName = t.ShortName, Description = t.Description, ContactEmail = t.ContactEmail,
            SocialUrl = t.SocialUrl, PrimaryColor = t.PrimaryColor, LogoPath = t.LogoPath, RowVersion = Convert.ToBase64String(t.RowVersion)
        });
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Info(MyTeamInfoViewModel model)
    {
        if (!await db.CanManageTeamAsync(User, model.TeamId)) return Forbid();
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == model.TeamId);
        if (team == null) return NotFound();
        if (model.Logo is { Length: > 0 } && FileUploadHelper.ValidateImage(model.Logo) is { } err) ModelState.AddModelError(nameof(model.Logo), err);
        // Same rule as team applications: no two teams with one name in the same area. The slug (page URL) stays.
        var newName = model.NameAr?.Trim() ?? "";
        if (newName != team.NameAr && await db.Teams.AnyAsync(t => t.Id != team.Id && t.NameAr == newName
                && t.TeamAreas.Any(ta => db.TeamAreas.Any(mine => mine.TeamId == team.Id && mine.AreaId == ta.AreaId))))
            ModelState.AddModelError(nameof(model.NameAr), "يوجد فريق آخر بنفس الاسم في منطقة فريقك.");
        if (!ModelState.IsValid)
        {
            model.CurrentName = team.NameAr;
            model.LogoPath = team.LogoPath;
            return View(model);
        }

        db.SetOriginalRowVersion(team, model.RowVersion, t => t.RowVersion);
        string? oldLogo = null;
        if (model.Logo is { Length: > 0 })
        {
            oldLogo = team.LogoPath;
            team.LogoPath = await FileUploadHelper.SaveImageAsync(model.Logo, env, "teams");
        }
        var oldName = team.NameAr;
        team.NameAr = newName;
        team.NameEn = model.NameEn.Trim();
        team.ShortName = model.ShortName?.Trim();
        team.Description = model.Description?.Trim();
        team.ContactEmail = model.ContactEmail?.Trim();
        team.SocialUrl = model.SocialUrl?.Trim();
        team.PrimaryColor = model.PrimaryColor;
        db.AddAudit(HttpContext, "تحديث بيانات الفريق (مسؤول الفريق)", nameof(Team), team.Id,
            oldName != team.NameAr ? $"{oldName} ← {team.NameAr}" : team.NameAr);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (oldLogo != null) FileUploadHelper.Delete(env, team.LogoPath);
            TempData["Error"] = "تم تعديل بيانات الفريق من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(Info), new { id = model.TeamId });
        }
        if (oldLogo != null && !await db.TeamApplications.AnyAsync(a => a.LogoPath == oldLogo)) FileUploadHelper.Delete(env, oldLogo);

        TempData["Success"] = "تم حفظ بيانات الفريق.";
        return RedirectToAction(nameof(Squad), new { id = model.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> AddPlayer(int id)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var model = new PlayerFormViewModel
        {
            TeamId = id, JoinedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            TeamName = await db.Teams.Where(t => t.Id == id).Select(t => t.NameAr).FirstOrDefaultAsync()
        };
        await PlayerHelper.FillListsAsync(db, model, includeTeams: false);
        return FormView("PlayerEdit", model);
    }

    /// <summary>
    /// Renders a form whose route {id} is the TEAM while the model's own Id is the player/match (0 when new).
    /// Tag helpers prefer ModelState values over the model, and ModelState holds the bound route "id": without
    /// clearing it the hidden Id field would carry the team id, so saving would target another record.
    /// </summary>
    private ViewResult FormView(string viewName, object model)
    {
        ModelState.Clear();
        return View(viewName, model);
    }

    [HttpGet]
    public async Task<IActionResult> EditPlayer(int id, int playerId)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var tp = await db.TeamPlayers.AsNoTracking().Include(x => x.Player).Include(x => x.Team)
            .FirstOrDefaultAsync(x => x.TeamId == id && x.PlayerId == playerId && x.IsActive);
        if (tp == null) return NotFound();
        var p = tp.Player;
        var model = new PlayerFormViewModel
        {
            Id = p.Id, FullNameAr = p.FullNameAr, FullNameEn = p.FullNameEn, DateOfBirth = p.DateOfBirth, Nationality = p.Nationality,
            PositionId = p.PositionId, Bio = p.Bio, ExistingPhotoPath = p.PhotoPath, RowVersion = Convert.ToBase64String(p.RowVersion),
            TeamId = id, TeamName = tp.Team.NameAr, ShirtNumber = tp.ShirtNumber, IsCaptain = tp.IsCaptain
        };
        await PlayerHelper.FillListsAsync(db, model, includeTeams: false);
        return FormView("PlayerEdit", model);
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> SavePlayer(PlayerFormViewModel model)
    {
        if (model.TeamId is not > 0 || !await db.CanManageTeamAsync(User, model.TeamId.Value)) return Forbid();
        // Editing is only allowed for players currently in this team.
        if (model.Id != 0 && !await db.TeamPlayers.AnyAsync(tp => tp.PlayerId == model.Id && tp.TeamId == model.TeamId && tp.IsActive))
            return Forbid();

        await PlayerHelper.ValidateAsync(db, model, ModelState);
        if (!ModelState.IsValid)
        {
            model.TeamName = await db.Teams.Where(t => t.Id == model.TeamId).Select(t => t.NameAr).FirstOrDefaultAsync();
            await PlayerHelper.FillListsAsync(db, model, includeTeams: false);
            return View("PlayerEdit", model);
        }

        try
        {
            var player = await PlayerHelper.SaveAsync(db, env, model);
            await db.SaveChangesAsync();
            db.AddAudit(HttpContext, model.Id == 0 ? "إضافة لاعب (مسؤول الفريق)" : "تعديل لاعب (مسؤول الفريق)", nameof(Player), player.Id, player.FullNameAr);
            await db.SaveChangesAsync();
            TempData["Success"] = "تم حفظ بيانات اللاعب.";
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل بيانات اللاعب من مستخدم آخر. أعد المحاولة.";
        }
        return RedirectToAction(nameof(Squad), new { id = model.TeamId });
    }

    /// <summary>Signs a player who currently has no team.</summary>
    [HttpPost]
    public async Task<IActionResult> SignFreeAgent(int id, int playerId, int? shirtNumber)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        if (await db.TeamPlayers.AnyAsync(tp => tp.PlayerId == playerId && tp.IsActive))
        {
            TempData["Error"] = "هذا اللاعب مسجل في فريق آخر. يتم الانتقال بين الفرق عن طريق إدارة المنصة.";
            return RedirectToAction(nameof(Squad), new { id });
        }
        string? error = null;
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            error = await PlayerHelper.TransferAsync(db, playerId, id, DateOnly.FromDateTime(DateTime.UtcNow), shirtNumber, "تسجيل لاعب حر", UserId);
            if (error != null) return;
            db.AddAudit(HttpContext, "تسجيل لاعب حر", nameof(Team), id, playerId.ToString());
            await db.SaveChangesAsync();
        });
        if (error != null) TempData["Error"] = error; else TempData["Success"] = "تم ضم اللاعب إلى الفريق.";
        return RedirectToAction(nameof(Squad), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ReleasePlayer(int id, int playerId)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        if (!await db.TeamPlayers.AnyAsync(tp => tp.PlayerId == playerId && tp.TeamId == id && tp.IsActive)) return NotFound();
        string? error = null;
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            error = await PlayerHelper.TransferAsync(db, playerId, null, DateOnly.FromDateTime(DateTime.UtcNow), null, "إنهاء ارتباط من مسؤول الفريق", UserId);
            if (error != null) return;
            db.AddAudit(HttpContext, "إنهاء ارتباط لاعب (مسؤول الفريق)", nameof(Team), id, playerId.ToString());
            await db.SaveChangesAsync();
        });
        if (error != null) TempData["Error"] = error; else TempData["Success"] = "تم إنهاء ارتباط اللاعب. يبقى سجله ومشاركاته محفوظة.";
        return RedirectToAction(nameof(Squad), new { id });
    }

    /// <summary>
    /// Deletes a player the team added by mistake. Only possible while the player has no record anywhere
    /// (match events, lineups, best-player awards, another team): otherwise the player's history and other teams'
    /// statistics depend on it, and the manager ends the player's membership instead (ReleasePlayer).
    /// Soft delete: the row stays for referential integrity and is hidden everywhere.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeletePlayer(int id, int playerId)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.TeamPlayers.Any(tp => tp.TeamId == id && tp.IsActive));
        if (player == null) return NotFound();

        var hasHistory = await db.MatchEvents.AnyAsync(e => e.PlayerId == playerId || e.RelatedPlayerId == playerId)
                         || await db.MatchLineups.AnyAsync(l => l.PlayerId == playerId)
                         || await db.MatchReports.AnyAsync(r => r.BestPlayerId == playerId)
                         || await db.TeamPlayers.AnyAsync(tp => tp.PlayerId == playerId && tp.TeamId != id);
        if (hasHistory)
        {
            TempData["Error"] = $"لا يمكن حذف {player.FullNameAr} لأن له سجلاً في المباريات أو مع فريق آخر. أنهِ ارتباطه بالفريق بدلاً من ذلك، ويبقى سجله محفوظاً.";
            return RedirectToAction(nameof(Squad), new { id });
        }

        await db.InTransactionAsync(async () =>
        {
            await db.TeamPlayers.Where(tp => tp.PlayerId == playerId).ExecuteDeleteAsync();
            await db.UserFollows.Where(f => f.PlayerId == playerId).ExecuteDeleteAsync();
            db.Players.Remove(player); // soft delete
            db.AddAudit(HttpContext, "حذف لاعب (مسؤول الفريق)", nameof(Player), playerId, player.FullNameAr);
            await db.SaveChangesAsync();
        });
        TempData["Success"] = $"تم حذف {player.FullNameAr}.";
        return RedirectToAction(nameof(Squad), new { id });
    }

    // ---------------- matches ----------------

    [HttpGet]
    public async Task<IActionResult> Matches(int id)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        var all = db.Matches.AsNoTracking().Where(m => m.HomeTeamId == id || m.AwayTeamId == id);
        var model = new MyTeamMatchesViewModel
        {
            TeamId = id, NameAr = team.NameAr, LogoPath = team.LogoPath,
            Upcoming = await all.Where(m => m.Status != MatchStatus.Finished)
                .OrderByDescending(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime).ThenBy(m => m.KickoffAt).ToRows().ToListAsync(),
            Played = await all.Where(m => m.Status == MatchStatus.Finished).OrderByDescending(m => m.KickoffAt).Take(30).ToRows().ToListAsync(),
            Operable = (await db.OperableMatches(User).Where(m => m.HomeTeamId == id || m.AwayTeamId == id).Select(m => m.Id).ToListAsync()).ToHashSet(),
            Organized = (await db.OrganizedMatches(User, id).Select(m => m.Id).ToListAsync()).ToHashSet()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateMatch(int id)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var model = new TeamMatchFormViewModel { TeamId = id, KickoffLocal = DateTime.UtcNow.ToMuscat().Date.AddDays(1).AddHours(17) };
        return FormView("MatchEdit", await FillMatchFormAsync(model));
    }

    [HttpGet]
    public async Task<IActionResult> EditMatch(int id, int matchId)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var m = await db.OrganizedMatches(User, id).AsNoTracking().Include(x => x.Officials).FirstOrDefaultAsync(x => x.Id == matchId);
        if (m == null) return NotFound();
        var isHome = m.HomeTeamId == id;
        var model = new TeamMatchFormViewModel
        {
            Id = m.Id, TeamId = id, IsHome = isHome, OpponentTeamId = isHome ? m.AwayTeamId : m.HomeTeamId,
            KickoffLocal = m.KickoffAt.ToMuscat(), VenueId = m.VenueId, Status = m.Status, Notes = m.Notes,
            IsTournament = m.TournamentName != null, TournamentName = m.TournamentName,
            RefereeName = m.Officials.FirstOrDefault(o => o.Role == MatchOfficialRole.Referee)?.Name,
            HasStarted = m.Status is not (MatchStatus.Scheduled or MatchStatus.Postponed or MatchStatus.Cancelled)
                         || await db.MatchEvents.AnyAsync(e => e.MatchId == matchId),
            RowVersion = Convert.ToBase64String(m.RowVersion)
        };
        return FormView("MatchEdit", await FillMatchFormAsync(model));
    }

    [HttpPost]
    public async Task<IActionResult> SaveMatch(TeamMatchFormViewModel model)
    {
        var id = model.TeamId;
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        Match? match = null;
        if (model.Id != 0)
        {
            match = await db.OrganizedMatches(User, id).Include(m => m.Officials).FirstOrDefaultAsync(m => m.Id == model.Id);
            if (match == null) return NotFound();
            if (match.Status is not (MatchStatus.Scheduled or MatchStatus.Postponed or MatchStatus.Cancelled) || await db.MatchEvents.AnyAsync(e => e.MatchId == match.Id))
            {
                TempData["Error"] = "بدأت المباراة؛ تُدار الآن من شاشة التسجيل المباشر.";
                return RedirectToAction(nameof(Matches), new { id });
            }
        }

        var guestName = model.OpponentGuestName?.Trim();
        if (string.IsNullOrEmpty(guestName)) guestName = null;
        if (model.OpponentTeamId == null && guestName == null)
            ModelState.AddModelError(nameof(model.OpponentTeamId), "اختر الخصم أو اكتب اسمه.");
        if (model.OpponentTeamId != null && guestName != null)
            ModelState.AddModelError(nameof(model.OpponentGuestName), "اختر الخصم من القائمة أو اكتب اسمه، وليس الاثنين.");
        if (model.OpponentTeamId == id) ModelState.AddModelError(nameof(model.OpponentTeamId), "لا يمكن أن يلعب الفريق ضد نفسه.");
        if (model.OpponentTeamId is { } opp && !await db.Teams.AnyAsync(t => t.Id == opp && (t.Status == TeamStatus.Approved || t.Status == TeamStatus.Guest)))
            ModelState.AddModelError(nameof(model.OpponentTeamId), "الخصم غير صالح.");
        // Optional: friendly by default; a tournament needs its name.
        var tournament = model.IsTournament ? model.TournamentName?.Trim() : null;
        if (string.IsNullOrEmpty(tournament)) tournament = null;
        if (model.IsTournament && tournament == null)
            ModelState.AddModelError(nameof(model.TournamentName), "اكتب اسم البطولة، أو اختر «ودية».");
        if (!FriendlyStatuses.Contains(model.Status)) ModelState.AddModelError(nameof(model.Status), "الحالة غير صالحة.");
        if (model.Id == 0 && model.Status != MatchStatus.Scheduled) model.Status = MatchStatus.Scheduled;
        if (model.KickoffLocal is { } k && Math.Abs((k - DateTime.UtcNow.ToMuscat()).TotalDays) > 366)
            ModelState.AddModelError(nameof(model.KickoffLocal), "الموعد يجب أن يكون خلال سنة من اليوم.");
        if (model.VenueId != null && !await db.Venues.AnyAsync(v => v.Id == model.VenueId)) ModelState.AddModelError(nameof(model.VenueId), "الملعب غير صالح.");
        if (!await db.Teams.AnyAsync(t => t.Id == id && t.Status == TeamStatus.Approved))
            ModelState.AddModelError(string.Empty, "يجب أن يكون فريقك معتمداً لإضافة مباريات.");
        if (!ModelState.IsValid) return View("MatchEdit", await FillMatchFormAsync(model));

        var isNew = match == null;
        var oldKickoff = match?.KickoffAt;
        var oldStatus = match?.Status;
        try
        {
            await db.InTransactionAsync(async () =>
            {
                var opponentId = model.OpponentTeamId ?? await GuestTeamIdAsync(guestName!);
                if (isNew)
                {
                    match = new Match { SportId = SeedData.FootballSportId, OrganizerTeamId = id };
                    db.Matches.Add(match);
                }
                else db.SetOriginalRowVersion(match!, model.RowVersion, m => m.RowVersion);

                match!.HomeTeamId = model.IsHome ? id : opponentId;
                match.AwayTeamId = model.IsHome ? opponentId : id;
                match.KickoffAt = model.KickoffLocal!.Value.FromMuscat();
                match.VenueId = model.VenueId;
                match.Status = model.Status;
                match.Notes = model.Notes?.Trim();
                match.TournamentName = tournament;
                var referee = match.Officials.FirstOrDefault(o => o.Role == MatchOfficialRole.Referee);
                if (string.IsNullOrWhiteSpace(model.RefereeName)) { if (referee != null) db.MatchOfficials.Remove(referee); }
                else if (referee == null) match.Officials.Add(new MatchOfficial { Role = MatchOfficialRole.Referee, Name = model.RefereeName.Trim() });
                else referee.Name = model.RefereeName.Trim();

                await db.SaveChangesAsync();
                db.AddAudit(HttpContext, isNew ? "إضافة مباراة ودية (مسؤول الفريق)" : "تعديل مباراة ودية (مسؤول الفريق)", nameof(Match), match.Id,
                    $"{match.Status.GetDisplayName()} — {match.KickoffAt.ToMuscatString()}");
                await db.SaveChangesAsync();
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل المباراة من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(EditMatch), new { id, matchId = model.Id });
        }

        await NotifyMatchSavedAsync(match!, isNew, oldKickoff, oldStatus);
        await hub.Clients.Groups(LiveHub.MatchGroup(match!.Id), LiveHub.LiveGroup).SendAsync("matchChanged", new { matchId = match.Id });
        TempData["Success"] = isNew ? "تمت إضافة المباراة. افتح شاشة التسجيل المباشر عند بدايتها." : "تم حفظ المباراة.";
        return RedirectToAction(nameof(Matches), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMatch(int id, int matchId)
    {
        if (!await db.CanManageTeamAsync(User, id)) return Forbid();
        var match = await db.OrganizedMatches(User, id).FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) return NotFound();
        if (match.Status is not (MatchStatus.Scheduled or MatchStatus.Postponed or MatchStatus.Cancelled) || await db.MatchEvents.AnyAsync(e => e.MatchId == matchId))
        {
            TempData["Error"] = "لا يمكن حذف مباراة بدأت أو سُجلت لها أحداث. يمكنك إلغاء آخر حدث من شاشة التسجيل.";
            return RedirectToAction(nameof(Matches), new { id });
        }
        await db.InTransactionAsync(async () =>
        {
            await db.MatchOfficials.Where(o => o.MatchId == matchId).ExecuteDeleteAsync();
            await db.MatchLineups.Where(l => l.MatchId == matchId).ExecuteDeleteAsync();
            await db.UserFollows.Where(f => f.MatchId == matchId).ExecuteDeleteAsync();
            db.Matches.Remove(match); // soft delete
            db.AddAudit(HttpContext, "حذف مباراة ودية (مسؤول الفريق)", nameof(Match), matchId, null);
            await db.SaveChangesAsync();
        });
        await hub.Clients.Group(LiveHub.LiveGroup).SendAsync("matchChanged", new { matchId });
        TempData["Success"] = "تم حذف المباراة.";
        return RedirectToAction(nameof(Matches), new { id });
    }

    /// <summary>Reuses a guest team with the same name, or creates one (hidden from public team lists).</summary>
    private async Task<int> GuestTeamIdAsync(string name)
    {
        var existing = await db.Teams.Where(t => t.Status == TeamStatus.Guest && t.NameAr == name).Select(t => (int?)t.Id).FirstOrDefaultAsync();
        if (existing != null) return existing.Value;
        var team = new Team
        {
            SportId = SeedData.FootballSportId, NameAr = name, NameEn = name, Status = TeamStatus.Guest,
            Slug = await SlugHelper.GenerateUniqueAsync("guest-" + Guid.NewGuid().ToString("N")[..8], s => db.Teams.IgnoreQueryFilters().AnyAsync(t => t.Slug == s), "guest")
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        db.AddAudit(HttpContext, "إضافة فريق ضيف", nameof(Team), team.Id, name);
        return team.Id;
    }

    private async Task<TeamMatchFormViewModel> FillMatchFormAsync(TeamMatchFormViewModel model)
    {
        var id = model.TeamId;
        model.TeamName = await db.Teams.Where(t => t.Id == id).Select(t => t.NameAr).FirstOrDefaultAsync();
        // Platform teams, plus guest teams this team has already played (so they can be picked again).
        var guestIds = db.Matches.Where(m => m.OrganizerTeamId == id).Select(m => m.HomeTeamId == id ? m.AwayTeamId : m.HomeTeamId);
        model.Opponents = await db.Teams.AsNoTracking()
            .Where(t => t.Id != id && (t.Status == TeamStatus.Approved || (t.Status == TeamStatus.Guest && guestIds.Contains(t.Id))))
            .OrderBy(t => t.Status).ThenBy(t => t.NameAr)
            .Select(t => new SelectListItem(t.Status == TeamStatus.Guest ? t.NameAr + " (فريق ضيف)" : t.NameAr, t.Id.ToString())).ToListAsync();
        model.Venues = await db.Venues.AsNoTracking().OrderBy(v => v.NameAr).Select(v => new SelectListItem(v.NameAr, v.Id.ToString())).ToListAsync();
        return model;
    }

    /// <summary>Followers of both teams hear about a new friendly; match followers about a new time, postponement or cancellation.</summary>
    private async Task NotifyMatchSavedAsync(Match match, bool isNew, DateTime? oldKickoff, MatchStatus? oldStatus)
    {
        try
        {
            var title = await db.Matches.Where(m => m.Id == match.Id).Select(m => m.HomeTeam.NameAr + " × " + m.AwayTeam.NameAr).FirstAsync();
            var when = match.KickoffAt.ToMuscatString("dddd yyyy/MM/dd HH:mm");
            if (isNew)
                await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id, NotificationType.MatchScheduled,
                    match.TournamentName != null ? $"مباراة جديدة في {match.TournamentName}: {title}" : $"مباراة ودية جديدة: {title}",
                    when);
            else if (oldStatus != match.Status && match.Status is MatchStatus.Postponed or MatchStatus.Cancelled)
                await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id, NotificationType.MatchRescheduled,
                    $"{(match.Status == MatchStatus.Postponed ? "تأجيل" : "إلغاء")} مباراة: {title}", match.Notes);
            else if (oldKickoff != match.KickoffAt && match.Status == MatchStatus.Scheduled)
                await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id, NotificationType.MatchRescheduled,
                    $"تغيير موعد: {title}", $"الموعد الجديد: {when}");
        }
        catch (Exception ex)
        {
            // The match is saved; a notification failure must not turn into an error for the manager.
            logger.LogWarning(ex, "Notification fan-out failed for match {MatchId}", match.Id);
        }
    }
}
