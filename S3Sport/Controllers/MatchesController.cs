using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Public match center (by day, live) and the live match page.</summary>
public class MatchesController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date, bool live = false, bool all = false)
    {
        var region = CurrentRegion.FromRequest(Request);
        var allRegions = all || region == null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.ToMuscat());
        var day = date ?? today;

        var query = db.Matches.AsNoTracking().Visible();
        if (!allRegions) query = query.InRegion(region);
        var liveQuery = query.Where(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime);

        if (live) query = liveQuery;
        else
        {
            var from = day.ToDateTime(TimeOnly.MinValue).FromMuscat();
            var to = from.AddDays(1);
            query = query.Where(m => m.KickoffAt >= from && m.KickoffAt < to);
        }

        var rows = await query.OrderBy(m => m.KickoffAt).ToRows().ToListAsync();
        var userId = User.GetUserId();
        var ids = rows.Select(r => r.Id).ToList();
        return View(new MatchCenterViewModel
        {
            Date = day,
            Days = Enumerable.Range(-3, 7).Select(d => today.AddDays(d)).ToList(),
            LiveOnly = live,
            AllRegions = allRegions,
            HasRegion = region != null,
            RegionName = region == null ? null : await region.GetDisplayNameAsync(db),
            LiveCount = await liveQuery.CountAsync(),
            Groups = GroupByCompetition(rows),
            FollowedMatchIds = userId == null ? [] : (await db.UserFollows
                .Where(f => f.UserId == userId && f.MatchId != null && ids.Contains(f.MatchId.Value))
                .Select(f => f.MatchId!.Value).ToListAsync()).ToHashSet()
        });
    }

    [HttpGet("/matches/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var info = await db.Matches.AsNoTracking().Visible().Where(m => m.Id == id).ToRows().FirstOrDefaultAsync();
        if (info == null) return NotFound();

        var extra = await db.Matches.AsNoTracking().Where(m => m.Id == id).Select(m => new
        {
            m.HomeTeamId, m.AwayTeamId, m.Notes,
            Stage = m.Stage != null ? m.Stage.NameAr : null,
            VenueAddress = m.Venue != null ? m.Venue.Address : null,
            Officials = m.Officials.OrderBy(o => o.Role).Select(o => new { o.Role, o.Name }).ToList(),
            BestPlayerId = m.Report != null ? m.Report.BestPlayerId : null,
            BestPlayer = m.Report != null && m.Report.BestPlayer != null ? m.Report.BestPlayer.FullNameAr : null,
            Attendance = m.Report != null ? m.Report.Attendance : null,
            Summary = m.Report != null ? m.Report.Summary : null
        }).FirstAsync();

        var lineups = await db.MatchLineups.AsNoTracking().Where(l => l.MatchId == id)
            .OrderByDescending(l => l.IsStarter).ThenBy(l => l.ShirtNumber ?? 999)
            .Select(l => new
            {
                l.TeamId,
                Row = new LineupRow
                {
                    PlayerId = l.PlayerId, Name = l.Player.FullNameAr, Number = l.ShirtNumber,
                    Position = l.Position != null ? l.Position.NameAr : l.Player.Position != null ? l.Player.Position.NameAr : null,
                    PositionCode = l.Position != null ? l.Position.Code : l.Player.Position != null ? l.Player.Position.Code : null,
                    PhotoPath = l.Player.PhotoPath, IsStarter = l.IsStarter, IsCaptain = l.IsCaptain, PitchOrder = l.PitchOrder
                }
            }).ToListAsync();

        // Previous meetings between the two teams (any competition), most recent first.
        var meetings = db.Matches.AsNoTracking().Visible()
            .Where(m => m.Id != id && m.Status == MatchStatus.Finished &&
                        ((m.HomeTeamId == extra.HomeTeamId && m.AwayTeamId == extra.AwayTeamId) ||
                         (m.HomeTeamId == extra.AwayTeamId && m.AwayTeamId == extra.HomeTeamId)))
            .OrderByDescending(m => m.KickoffAt);
        var h2hRows = await meetings.Take(5).ToRows().ToListAsync();
        var h2hWinners = await meetings.Select(m => m.WinnerTeamId != null ? m.WinnerTeamId
            : m.HomeScore > m.AwayScore ? m.HomeTeamId : m.HomeScore < m.AwayScore ? m.AwayTeamId : (int?)null).ToListAsync();
        int Won(int team) => h2hWinners.Count(w => w == team);
        var draws = h2hWinners.Count(w => w == null);

        var model = new MatchDetailsViewModel
        {
            Info = info,
            Live = (await MatchLiveHelper.BuildDtoAsync(db, id))!,
            StageName = extra.Stage,
            VenueAddress = extra.VenueAddress,
            Notes = extra.Notes,
            Officials = extra.Officials.Select(o => (o.Role, o.Name)).ToList(),
            BestPlayerId = extra.BestPlayerId, BestPlayerName = extra.BestPlayer, Attendance = extra.Attendance, Summary = extra.Summary,
            HomeLineup = lineups.Where(l => l.TeamId == extra.HomeTeamId).Select(l => l.Row).ToList(),
            AwayLineup = lineups.Where(l => l.TeamId == extra.AwayTeamId).Select(l => l.Row).ToList(),
            HeadToHead = h2hRows,
            HeadToHeadSummary = (Won(extra.HomeTeamId), draws, Won(extra.AwayTeamId)),
            Follow = await FollowHelper.BuildAsync(db, User.GetUserId(), FollowTargetType.Match, id, compact: false),
            CanOperate = User.Identity?.IsAuthenticated == true && await db.CanOperateMatchAsync(User, id),
            Photos = await db.MatchPhotos.AsNoTracking().Where(p => p.MatchId == id).OrderByDescending(p => p.Id)
                .Select(p => new MatchPhotoItem { Id = p.Id, Path = p.Path, Caption = p.Caption, TeamId = p.TeamId, TeamName = p.Team.NameAr })
                .ToListAsync(),
            CanModeratePhotos = User.IsAdmin()
        };
        var uploadTeams = await MatchPhotosController.UploadTeamIdsAsync(db, User, extra.HomeTeamId, extra.AwayTeamId);
        model.PhotoUploadTeams = uploadTeams.Select(t => (t, t == extra.HomeTeamId ? info.HomeName : info.AwayName)).ToList();
        return View(model);
    }

    /// <summary>Live state for clients that cannot use WebSockets (polling fallback).</summary>
    [HttpGet("/matches/{id:int}/state")]
    public async Task<IActionResult> State(int id)
    {
        if (!await db.Matches.Visible().AnyAsync(m => m.Id == id)) return NotFound();
        return Json(await MatchLiveHelper.BuildDtoAsync(db, id));
    }

    public static List<CompetitionMatchGroup> GroupByCompetition(IEnumerable<MatchRow> rows) =>
        rows.GroupBy(r => new { Name = r.EventName, r.CompetitionSlug, r.CompetitionLogo })
            .OrderBy(g => g.Key.Name == null).ThenBy(g => g.Key.Name)
            .Select(g => new CompetitionMatchGroup
            {
                Name = g.Key.Name ?? "مباريات ودية",
                Slug = g.Key.CompetitionSlug,
                Logo = g.Key.CompetitionLogo,
                Matches = g.OrderByDescending(m => m.IsLive).ThenBy(m => m.KickoffAt).ToList()
            }).ToList();
}
