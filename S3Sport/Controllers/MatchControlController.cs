using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Hubs;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>
/// Match operator screen: live events, lineups, corrections and the match report.
/// Only admins, the competition's tournament managers, and the match's assigned operator may use it.
/// Every write runs in a SQL transaction holding an update lock on the match row, so concurrent
/// operators are serialised and the score always equals the sum of the recorded events.
/// </summary>
[Authorize(Policy = AppPolicies.OperateMatches)]
public class MatchControlController(ApplicationDbContext db, IHubContext<LiveHub> hub, ILogger<MatchControlController> logger) : Controller
{
    private static readonly JsonSerializerOptions SnapshotJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var mine = db.OperableMatches(User).AsNoTracking();
        var now = DateTime.UtcNow;
        return View(new OperatorMatchListViewModel
        {
            Live = await mine.Where(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime).OrderBy(m => m.KickoffAt).ToRows().ToListAsync(),
            Upcoming = await mine.Where(m => (m.Status == MatchStatus.Scheduled || m.Status == MatchStatus.Postponed) && m.KickoffAt >= now.AddDays(-2)) // late or overdue kick-offs stay visible
                .OrderBy(m => m.KickoffAt).Take(30).ToRows().ToListAsync(),
            Recent = await mine.Where(m => m.Status == MatchStatus.Finished).OrderByDescending(m => m.KickoffAt).Take(15).ToRows().ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        var m = await db.Matches.AsNoTracking()
            .Include(x => x.HomeTeam).Include(x => x.AwayTeam).Include(x => x.Venue).Include(x => x.Stage)
            .Include(x => x.CompetitionSeason).ThenInclude(c => c!.Competition).Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();

        var lineup = await db.MatchLineups.AsNoTracking().Where(l => l.MatchId == id).ToListAsync();
        var model = new OperatorViewModel
        {
            MatchId = m.Id, HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId,
            HomeName = m.HomeTeam.NameAr, HomeLogo = m.HomeTeam.LogoPath, AwayName = m.AwayTeam.NameAr, AwayLogo = m.AwayTeam.LogoPath,
            CompetitionName = m.CompetitionSeason?.Competition.NameAr ?? m.TournamentName,RoundName = m.RoundName, VenueName = m.Venue?.NameAr,
            IsKnockout = m.Stage?.StageType == StageType.Knockout, Status = m.Status,
            HomeIsGuest = m.HomeTeam.Status == TeamStatus.Guest, AwayIsGuest = m.AwayTeam.Status == TeamStatus.Guest,
            Live = (await MatchLiveHelper.BuildDtoAsync(db, id, includeCancelled: true))!,
            HomeSquad = await SquadAsync(m.HomeTeamId, id, lineup),
            AwaySquad = await SquadAsync(m.AwayTeamId, id, lineup),
            Positions = await FootballLinesAsync(),
            BestPlayerId = m.Report?.BestPlayerId, Attendance = m.Report?.Attendance, Summary = m.Report?.Summary,
            Revisions = await db.MatchEventRevisions.AsNoTracking().Where(r => r.MatchEvent.MatchId == id)
                .OrderByDescending(r => r.Id).Take(30)
                .Select(r => new RevisionRow
                {
                    EventId = r.MatchEventId, Action = r.Action, Reason = r.Reason, CreatedAt = r.CreatedAt,
                    ChangedBy = r.ChangedByUser != null ? r.ChangedByUser.FullName : null, OldValues = r.OldValuesJson, NewValues = r.NewValuesJson
                }).ToListAsync()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> State(int id)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        return Json(await MatchLiveHelper.BuildDtoAsync(db, id, includeCancelled: true));
    }

    /// <summary>Records one event. Idempotent per ClientRequestId: a repeated click returns the stored result.</summary>
    [HttpPost]
    public async Task<IActionResult> AddEvent(int id, [FromForm] EventInput input)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        if (!ModelState.IsValid) return Fail("بيانات الحدث غير مكتملة.");

        var type = input.EventType!.Value;
        var hasRoster = await NormalizePlayersAsync(input, type, id);
        string? error = null;
        var duplicate = false;

        try
        {
            await db.InTransactionAsync(async () =>
            {
                db.ChangeTracker.Clear();
                error = null; duplicate = false;
                // Serialise writers on this match (the lock is held until commit).
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Matches WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}");

                if (await db.MatchEvents.AnyAsync(e => e.MatchId == id && e.ClientRequestId == input.ClientRequestId))
                {
                    duplicate = true;
                    return;
                }
                var match = await db.Matches.Include(x => x.Stage).FirstAsync(x => x.Id == id);
                error = MatchLiveHelper.Validate(match, type, input.TeamId, input.PlayerId, input.RelatedPlayerId, input.PlayerName, input.RelatedPlayerName, hasRoster)
                        ?? await ValidatePlayersAsync(match, input);
                if (error != null) return;

                if (type == MatchEventType.MatchEnded && match.Stage?.StageType == StageType.Knockout && match.HomeScore == match.AwayScore)
                {
                    if (input.HomePenaltyScore is null || input.AwayPenaltyScore is null || input.HomePenaltyScore == input.AwayPenaltyScore)
                    {
                        error = "مباراة إقصائية متعادلة: أدخل نتيجة ركلات الترجيح (بدون تعادل).";
                        return;
                    }
                    match.HomePenaltyScore = input.HomePenaltyScore;
                    match.AwayPenaltyScore = input.AwayPenaltyScore;
                }

                var ev = new MatchEvent
                {
                    MatchId = id, EventType = type, ClientRequestId = input.ClientRequestId!.Value,
                    TeamId = MatchLiveHelper.IsStateEvent(type) ? null : input.TeamId,
                    PlayerId = MatchLiveHelper.IsStateEvent(type) ? null : input.PlayerId,
                    RelatedPlayerId = MatchLiveHelper.IsStateEvent(type) ? null : input.RelatedPlayerId,
                    PlayerNameText = input.PlayerName, RelatedPlayerNameText = input.RelatedPlayerName,
                    Minute = input.Minute ?? DefaultMinute(match), ExtraMinute = input.ExtraMinute,
                    Period = type == MatchEventType.MatchStarted ? 1 : type == MatchEventType.SecondHalfStarted ? 2 : Math.Max(1, match.CurrentPeriod),
                    Notes = input.Notes?.Trim(), CreatedByUserId = User.GetUserId(), CreatedAt = DateTime.UtcNow
                };
                db.MatchEvents.Add(ev);
                var events = await db.MatchEvents.Where(e => e.MatchId == id).ToListAsync();
                events.Add(ev);
                MatchLiveHelper.Recompute(match, events);
                await db.SaveChangesAsync();

                db.AddAudit(HttpContext, "حدث مباراة: " + type.GetDisplayName(), nameof(Match), id, $"{ev.Minute}' {match.HomeScore}-{match.AwayScore}");
                await db.SaveChangesAsync();

                if (type == MatchEventType.MatchEnded) await AfterResultChangedAsync(match);
            });
        }
        catch (DbUpdateException ex) when (IsDuplicateRequest(ex))
        {
            // Two identical submits raced past the existence check: the unique index kept just one.
            duplicate = true;
        }

        if (error != null) return Fail(error);
        var dto = await MatchLiveHelper.BuildDtoAsync(db, id, includeCancelled: true);
        if (!duplicate)
        {
            await BroadcastAsync(dto!);
            await NotifyAsync(id, type, input);
        }
        return Ok(new { ok = true, duplicate, state = dto });
    }

    /// <summary>Corrects an event (minute, team, players, notes) and keeps the previous values in the revision history.</summary>
    [HttpPost]
    public async Task<IActionResult> CorrectEvent(int id, int eventId, [FromForm] EventCorrectionInput input)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        if (!ModelState.IsValid) return Fail(ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "بيانات غير صالحة.");
        string? error = null;

        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            error = null;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Matches WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}");
            var match = await db.Matches.FirstAsync(m => m.Id == id);
            var ev = await db.MatchEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.MatchId == id);
            if (ev == null) { error = "الحدث غير موجود."; return; }
            if (ev.IsCancelled) { error = "الحدث ملغى."; return; }

            var isState = MatchLiveHelper.IsStateEvent(ev.EventType);
            var players = new EventInput
            {
                TeamId = input.TeamId, PlayerId = input.PlayerId, RelatedPlayerId = input.RelatedPlayerId,
                PlayerName = input.PlayerName, RelatedPlayerName = input.RelatedPlayerName, EventType = ev.EventType
            };
            if (!isState)
            {
                if (input.TeamId != match.HomeTeamId && input.TeamId != match.AwayTeamId) { error = "اختر الفريق."; return; }
                var hasRoster = await NormalizePlayersAsync(players, ev.EventType, id);
                var hasOut = players.PlayerId != null || players.PlayerName != null;
                var hasIn = players.RelatedPlayerId != null || players.RelatedPlayerName != null;
                if (ev.EventType == MatchEventType.Substitution && hasRoster && !(hasOut && hasIn)) { error = "التبديل يحتاج اللاعب الخارج واللاعب الداخل."; return; }
                error = await ValidatePlayersAsync(match, players);
                if (error != null) return;
            }

            var before = Snapshot(ev);
            ev.Minute = input.Minute;
            ev.ExtraMinute = input.ExtraMinute;
            ev.Notes = input.Notes?.Trim();
            if (!isState)
            {
                ev.TeamId = players.TeamId;
                ev.PlayerId = players.PlayerId;
                ev.RelatedPlayerId = players.RelatedPlayerId;
                ev.PlayerNameText = players.PlayerName;
                ev.RelatedPlayerNameText = players.RelatedPlayerName;
            }
            db.MatchEventRevisions.Add(new MatchEventRevision
            {
                MatchEventId = ev.Id, Action = EventRevisionAction.Updated, OldValuesJson = before, NewValuesJson = Snapshot(ev),
                Reason = input.Reason!.Trim(), ChangedByUserId = User.GetUserId()
            });
            MatchLiveHelper.Recompute(match, await db.MatchEvents.Where(e => e.MatchId == id).ToListAsync());
            db.AddAudit(HttpContext, "تصحيح حدث مباراة", nameof(Match), id, $"حدث {eventId}: {input.Reason}");
            await db.SaveChangesAsync();
            await AfterResultChangedAsync(match);
        });

        if (error != null) return Fail(error);
        var dto = await MatchLiveHelper.BuildDtoAsync(db, id, includeCancelled: true);
        await BroadcastAsync(dto!);
        return Ok(new { ok = true, state = dto });
    }

    /// <summary>Cancels (or restores) an event. State events can be undone too, e.g. a match ended by mistake.</summary>
    [HttpPost]
    public async Task<IActionResult> CancelEvent(int id, int eventId, string? reason, bool restore = false)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        if (string.IsNullOrWhiteSpace(reason)) return Fail("سبب الإلغاء مطلوب.");
        string? error = null;

        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            error = null;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Matches WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}");
            var match = await db.Matches.FirstAsync(m => m.Id == id);
            var events = await db.MatchEvents.Where(e => e.MatchId == id).ToListAsync();
            var ev = events.FirstOrDefault(e => e.Id == eventId);
            if (ev == null) { error = "الحدث غير موجود."; return; }
            if (ev.IsCancelled != restore) { error = restore ? "الحدث غير ملغى." : "الحدث ملغى مسبقاً."; return; }

            // Only the latest state event can be undone, so the sequence start → half → second half → end stays valid.
            if (MatchLiveHelper.IsStateEvent(ev.EventType))
            {
                var activeStates = events.Where(e => !e.IsCancelled && MatchLiveHelper.IsStateEvent(e.EventType)).OrderBy(e => e.Id).ToList();
                if (!restore && activeStates.LastOrDefault()?.Id != ev.Id) { error = "يمكن التراجع عن آخر تغيير في حالة المباراة فقط."; return; }
                if (restore) { error = "لا يمكن استعادة حدث حالة؛ سجله من جديد."; return; }
            }

            ev.IsCancelled = !restore;
            db.MatchEventRevisions.Add(new MatchEventRevision
            {
                MatchEventId = ev.Id, Action = restore ? EventRevisionAction.Restored : EventRevisionAction.Cancelled,
                OldValuesJson = Snapshot(ev), Reason = reason.Trim(), ChangedByUserId = User.GetUserId()
            });
            MatchLiveHelper.Recompute(match, events);
            db.AddAudit(HttpContext, restore ? "استعادة حدث مباراة" : "إلغاء حدث مباراة", nameof(Match), id, $"حدث {eventId} ({ev.EventType.GetDisplayName()}): {reason}");
            await db.SaveChangesAsync();
            await AfterResultChangedAsync(match);
        });

        if (error != null) return Fail(error);
        var dto = await MatchLiveHelper.BuildDtoAsync(db, id, includeCancelled: true);
        await BroadcastAsync(dto!);
        return Ok(new { ok = true, state = dto });
    }

    /// <summary>Saves both lineups: starters and substitutes (players must belong to the team's current squad).</summary>
    [HttpPost]
    public async Task<IActionResult> SaveLineup(int id, List<int> homeStarters, List<int> homeBench, List<int> awayStarters, List<int> awayBench)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        var match = await db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (match == null) return NotFound();

        async Task<List<MatchLineup>> Build(int teamId, List<int> starters, List<int> bench)
        {
            var allowed = await db.TeamPlayers.Where(tp => tp.TeamId == teamId && tp.IsActive)
                .Select(tp => new { tp.PlayerId, tp.ShirtNumber, tp.PositionId, tp.IsCaptain }).ToListAsync();
            // Players already in this lineup keep their place on the pitch (line and order) set by the operator.
            var already = await db.MatchLineups.Where(l => l.MatchId == id && l.TeamId == teamId)
                .Select(l => new { l.PlayerId, l.PositionId, l.PitchOrder }).ToListAsync();
            return starters.Distinct().Select(p => (p, true)).Concat(bench.Distinct().Except(starters).Select(p => (p, false)))
                .Where(x => allowed.Any(a => a.PlayerId == x.p) || already.Any(l => l.PlayerId == x.p))
                .Select(x =>
                {
                    var a = allowed.FirstOrDefault(a => a.PlayerId == x.p);
                    var prev = already.FirstOrDefault(l => l.PlayerId == x.p);
                    return new MatchLineup
                    {
                        MatchId = id, TeamId = teamId, PlayerId = x.p, IsStarter = x.Item2, ShirtNumber = a?.ShirtNumber,
                        PositionId = prev != null ? prev.PositionId : a?.PositionId, PitchOrder = prev?.PitchOrder, IsCaptain = a?.IsCaptain ?? false
                    };
                }).ToList();
        }

        var lineup = (await Build(match.HomeTeamId, homeStarters, homeBench)).Concat(await Build(match.AwayTeamId, awayStarters, awayBench)).ToList();
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await db.MatchLineups.Where(l => l.MatchId == id).ExecuteDeleteAsync();
            db.MatchLineups.AddRange(lineup.Select(l => new MatchLineup
            {
                MatchId = l.MatchId, TeamId = l.TeamId, PlayerId = l.PlayerId, IsStarter = l.IsStarter,
                ShirtNumber = l.ShirtNumber, PositionId = l.PositionId, PitchOrder = l.PitchOrder, IsCaptain = l.IsCaptain
            }));
            db.AddAudit(HttpContext, "حفظ تشكيلة مباراة", nameof(Match), id, $"{lineup.Count(l => l.IsStarter)} أساسي");
            await db.SaveChangesAsync();
            if (match.Status == MatchStatus.Finished && match.CompetitionSeasonId != null)
                await StandingsCalculator.RecalculateAsync(db, match.CompetitionSeasonId.Value);
        });

        await hub.Clients.Group(LiveHub.MatchGroup(id)).SendAsync("lineupChanged", new { matchId = id });
        TempData["Success"] = "تم حفظ التشكيلة.";
        return RedirectToAction(nameof(Index), new { id });
    }

    /// <summary>
    /// Moves starters on the pitch view after the lineup is out: each one's line (GK/DF/MF/FW) and order within it.
    /// Allowed at any time, even after the final whistle; only display fields change, so stats and standings are untouched.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SavePitch(int id, List<PitchSlot> slots)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        if (!await db.Matches.AnyAsync(m => m.Id == id)) return NotFound();

        var lines = (await FootballLinesAsync()).Select(p => p.Id).ToHashSet();
        var rows = await db.MatchLineups.Where(l => l.MatchId == id && l.IsStarter).ToListAsync();
        var changed = 0;
        foreach (var s in slots.GroupBy(s => s.PlayerId).Select(g => g.Last()))
        {
            var row = rows.FirstOrDefault(r => r.PlayerId == s.PlayerId);
            if (row == null || !lines.Contains(s.PositionId)) continue;
            row.PositionId = s.PositionId;
            row.PitchOrder = s.Order;
            changed++;
        }
        if (changed > 0)
        {
            db.AddAudit(HttpContext, "ترتيب التشكيلة على الملعب", nameof(Match), id, $"{changed} لاعب");
            await db.SaveChangesAsync();
            await hub.Clients.Group(LiveHub.MatchGroup(id)).SendAsync("lineupChanged", new { matchId = id });
        }
        TempData["Success"] = "تم حفظ ترتيب اللاعبين على الملعب.";
        return RedirectToAction(nameof(Index), new { id });
    }

    private Task<List<SportPosition>> FootballLinesAsync() =>
        db.SportPositions.AsNoTracking().Where(p => p.SportId == SeedData.FootballSportId && new[] { "GK", "DF", "MF", "FW" }.Contains(p.Code))
            .OrderBy(p => p.SortOrder).ToListAsync();

    [HttpPost]
    public async Task<IActionResult> SaveReport(int id, int? bestPlayerId, int? attendance, string? summary)
    {
        if (!await db.CanOperateMatchAsync(User, id)) return Forbid();
        var match = await db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (match == null) return NotFound();
        if (bestPlayerId != null && !await db.TeamPlayers.AnyAsync(tp => tp.PlayerId == bestPlayerId && (tp.TeamId == match.HomeTeamId || tp.TeamId == match.AwayTeamId))
            && !await db.MatchLineups.AnyAsync(l => l.MatchId == id && l.PlayerId == bestPlayerId))
        {
            TempData["Error"] = "أفضل لاعب يجب أن يكون من أحد الفريقين.";
            return RedirectToAction(nameof(Index), new { id });
        }
        if (attendance is < 0 or > 200000) attendance = null;

        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var report = await db.MatchReports.FirstOrDefaultAsync(r => r.MatchId == id);
            if (report == null) { report = new MatchReport { MatchId = id }; db.MatchReports.Add(report); }
            report.BestPlayerId = bestPlayerId;
            report.Attendance = attendance;
            report.Summary = summary?.Trim() is { Length: > 0 } s ? s[..Math.Min(s.Length, 4000)] : null;
            report.WrittenByUserId = User.GetUserId();
            db.AddAudit(HttpContext, "تقرير مباراة", nameof(Match), id, null);
            await db.SaveChangesAsync();
            if (match.Status == MatchStatus.Finished && match.CompetitionSeasonId != null)
                await StandingsCalculator.RecalculateAsync(db, match.CompetitionSeasonId.Value);
        });
        TempData["Success"] = "تم حفظ تقرير المباراة.";
        return RedirectToAction(nameof(Index), new { id });
    }

    // ---------------- helpers ----------------

    /// <summary>Validation / state errors: 400 with an Arabic message the operator screen shows.</summary>
    private BadRequestObjectResult Fail(string message) => BadRequest(new { ok = false, error = message });

    private async Task<string?> ValidatePlayersAsync(Match match, EventInput input)
    {
        foreach (var playerId in new[] { input.PlayerId, input.RelatedPlayerId }.Where(p => p != null))
        {
            // A player counts for the team if they are in its squad or in its lineup for this match.
            var belongs = await db.TeamPlayers.AnyAsync(tp => tp.PlayerId == playerId && tp.TeamId == input.TeamId && tp.IsActive)
                          || await db.MatchLineups.AnyAsync(l => l.MatchId == match.Id && l.PlayerId == playerId && l.TeamId == input.TeamId);
            if (!belongs) return "اللاعب المختار لا ينتمي للفريق.";
        }
        return null;
    }

    private static int DefaultMinute(Match m)
    {
        if (m.CurrentPeriodStartedAt is not { } start) return m.CurrentPeriod >= 2 ? 46 : 1;
        var elapsed = (int)Math.Floor((DateTime.UtcNow - start).TotalMinutes) + 1;
        return Math.Clamp(elapsed + (m.CurrentPeriod >= 2 ? 45 : 0), 1, 200);
    }

    private static string Snapshot(MatchEvent e) => JsonSerializer.Serialize(new
    {
        e.EventType, e.TeamId, e.PlayerId, e.RelatedPlayerId, e.PlayerNameText, e.RelatedPlayerNameText, e.Minute, e.ExtraMinute, e.Notes, e.IsCancelled
    }, SnapshotJson);

    /// <summary>
    /// A player is either picked from the team's registered players (an id, which keeps statistics linked) or typed as
    /// a shirt number / name for a player the platform doesn't know: an opponent that registered no players, a guest
    /// team, an unregistered substitute. An id wins over typed text; guest teams never carry ids. A bare number is
    /// stored as "رقم N". Returns whether the team has registered players for this match; without them, naming the
    /// players is optional for every event, substitutions included.
    /// </summary>
    private async Task<bool> NormalizePlayersAsync(EventInput input, MatchEventType type, int matchId)
    {
        static string? Clean(string? s)
        {
            var t = s?.Trim();
            if (string.IsNullOrEmpty(t)) return null;
            return t.All(char.IsDigit) ? $"رقم {t}" : t;
        }
        if (MatchLiveHelper.IsStateEvent(type) || input.TeamId == null)
        {
            input.PlayerName = input.RelatedPlayerName = null;
            return true;
        }
        var isGuest = await db.Teams.AnyAsync(t => t.Id == input.TeamId && t.Status == TeamStatus.Guest);
        if (isGuest) input.PlayerId = input.RelatedPlayerId = null;
        input.PlayerName = input.PlayerId != null ? null : Clean(input.PlayerName);
        input.RelatedPlayerName = input.RelatedPlayerId != null ? null : Clean(input.RelatedPlayerName);
        return !isGuest && (await db.TeamPlayers.AnyAsync(tp => tp.TeamId == input.TeamId && tp.IsActive)
                            || await db.MatchLineups.AnyAsync(l => l.MatchId == matchId && l.TeamId == input.TeamId));
    }

    private static bool IsDuplicateRequest(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("ClientRequestId", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Standings, statistics and knockout progression follow every finished-match change.</summary>
    private async Task AfterResultChangedAsync(Match match)
    {
        if (match.CompetitionSeasonId == null) return;
        await StandingsCalculator.RecalculateAsync(db, match.CompetitionSeasonId.Value);
        if (match.Status == MatchStatus.Finished) await EditionHelper.CheckKnockoutChampionAsync(db, match.Id);
    }

    private async Task BroadcastAsync(MatchLiveDto dto)
    {
        var summary = new { dto.MatchId, dto.Status, dto.StatusText, dto.HomeScore, dto.AwayScore, dto.HomePenaltyScore, dto.AwayPenaltyScore, dto.Period, dto.PeriodDurationMinutes, dto.PeriodStartedAt };
        var publicDto = new MatchLiveDto
        {
            MatchId = dto.MatchId, Status = dto.Status, StatusText = dto.StatusText, HomeTeamId = dto.HomeTeamId, AwayTeamId = dto.AwayTeamId,
            HomeScore = dto.HomeScore, AwayScore = dto.AwayScore, HomePenaltyScore = dto.HomePenaltyScore, AwayPenaltyScore = dto.AwayPenaltyScore,
            Period = dto.Period, PeriodDurationMinutes = dto.PeriodDurationMinutes, PeriodStartedAt = dto.PeriodStartedAt, KickoffAt = dto.KickoffAt,
            Events = dto.Events.Where(e => !e.IsCancelled).ToList()
        };
        try
        {
            await hub.Clients.Group(LiveHub.MatchGroup(dto.MatchId)).SendAsync("matchState", publicDto);
            await hub.Clients.Group(LiveHub.LiveGroup).SendAsync("scoreUpdate", summary);
        }
        catch (Exception ex)
        {
            // The event is committed; a broadcast failure must not turn into an error for the operator.
            logger.LogWarning(ex, "Live broadcast failed for match {MatchId}", dto.MatchId);
        }
    }

    private async Task NotifyAsync(int matchId, MatchEventType type, EventInput input)
    {
        var info = await db.Matches.AsNoTracking().Where(m => m.Id == matchId)
            .Select(m => new { Home = m.HomeTeam.NameAr, Away = m.AwayTeam.NameAr, m.HomeScore, m.AwayScore, m.HomePenaltyScore, m.AwayPenaltyScore })
            .FirstAsync();
        var score = $"{info.Home} {info.HomeScore} - {info.AwayScore} {info.Away}";
        // The stored event carries the minute the server filled in when the operator left it empty.
        var ev = await db.MatchEvents.AsNoTracking().Where(e => e.MatchId == matchId && e.ClientRequestId == input.ClientRequestId)
            .Select(e => new
            {
                e.Minute, Team = e.Team != null ? e.Team.NameAr : null,
                Player = e.Player != null ? e.Player.FullNameAr : e.PlayerNameText,
                Related = e.RelatedPlayer != null ? e.RelatedPlayer.FullNameAr : e.RelatedPlayerNameText
            }).FirstOrDefaultAsync();
        var minute = ev != null ? $" ({ev.Minute}')" : "";
        var who = ev?.Player != null ? $"{ev.Player} — {ev.Team}" : ev?.Team;
        (NotificationType Type, string Title, string? Message)? n = type switch
        {
            MatchEventType.MatchStarted => (NotificationType.MatchStarted, $"▶️ بدأت المباراة: {info.Home} × {info.Away}", null),
            MatchEventType.FirstHalfEnded => (NotificationType.HalfTime, $"⏸️ نهاية الشوط الأول: {score}", null),
            MatchEventType.SecondHalfStarted => (NotificationType.SecondHalfStarted, $"▶️ بداية الشوط الثاني: {score}", null),
            MatchEventType.MatchEnded => (NotificationType.MatchEnded, $"🏁 انتهت المباراة: {score}",
                info.HomePenaltyScore != null ? $"ركلات الترجيح: {info.HomePenaltyScore} - {info.AwayPenaltyScore}" : null),
            MatchEventType.Goal => (NotificationType.Goal, $"⚽ هدف! {score}", who + minute),
            MatchEventType.PenaltyGoal => (NotificationType.Goal, $"⚽ هدف من ركلة جزاء! {score}", who + minute),
            MatchEventType.OwnGoal => (NotificationType.Goal, $"⚽ هدف عكسي! {score}", who + minute),
            MatchEventType.MissedPenalty => (NotificationType.Penalty, $"❌ ركلة جزاء ضائعة: {ev?.Team}", (ev?.Player ?? "") + minute),
            MatchEventType.YellowCard => (NotificationType.Card, $"🟨 بطاقة صفراء: {ev?.Team}", (ev?.Player ?? "") + minute),
            MatchEventType.SecondYellowCard => (NotificationType.Card, $"🟥 طرد (إنذار ثانٍ): {ev?.Team}", (ev?.Player ?? "") + minute),
            MatchEventType.RedCard => (NotificationType.Card, $"🟥 بطاقة حمراء: {ev?.Team}", (ev?.Player ?? "") + minute),
            MatchEventType.Substitution => (NotificationType.Substitution, $"🔄 تبديل: {ev?.Team}",
                $"دخول {ev?.Related} وخروج {ev?.Player}{minute}"),
            _ => null
        };
        if (n == null) return;
        try
        {
            await NotificationHelper.NotifyMatchFollowersAsync(db, hub, matchId, n.Value.Type, n.Value.Title, n.Value.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification fan-out failed for match {MatchId}", matchId);
        }
    }


    private async Task<List<SquadPlayer>> SquadAsync(int teamId, int matchId, List<MatchLineup> lineup)
    {
        var squad = await db.TeamPlayers.AsNoTracking().Where(tp => tp.TeamId == teamId && tp.IsActive)
            .OrderBy(tp => tp.ShirtNumber ?? 999).ThenBy(tp => tp.Player.FullNameAr)
            .Select(tp => new SquadPlayer
            {
                Id = tp.PlayerId, Name = tp.Player.FullNameAr, Number = tp.ShirtNumber,
                Position = tp.Position != null ? tp.Position.NameAr : tp.Player.Position != null ? tp.Player.Position.NameAr : null,
                PositionId = tp.PositionId ?? tp.Player.PositionId
            }).ToListAsync();
        // Players who were in this match's lineup but have since left the team still appear.
        var missing = lineup.Where(l => l.TeamId == teamId && squad.All(s => s.Id != l.PlayerId)).Select(l => l.PlayerId).ToList();
        if (missing.Count > 0)
            squad.AddRange(await db.Players.AsNoTracking().Where(p => missing.Contains(p.Id))
                .Select(p => new SquadPlayer { Id = p.Id, Name = p.FullNameAr, PositionId = p.PositionId }).ToListAsync());
        foreach (var s in squad)
        {
            var l = lineup.FirstOrDefault(x => x.PlayerId == s.Id && x.TeamId == teamId);
            s.InLineup = l != null;
            s.IsStarter = l?.IsStarter ?? false;
            if (l != null) { s.PositionId = l.PositionId ?? s.PositionId; s.PitchOrder = l.PitchOrder; }
        }
        return squad;
    }
}
