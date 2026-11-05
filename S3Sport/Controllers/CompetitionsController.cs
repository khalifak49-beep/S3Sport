using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Public competitions: directory and competition page (standings, fixtures, results, statistics per season).</summary>
public class CompetitionsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool all = false)
    {
        var region = CurrentRegion.FromRequest(Request);
        var allRegions = all || region == null;
        var query = db.Competitions.AsNoTracking().Where(c => c.IsActive && c.Seasons.Any());
        if (!allRegions) query = query.InRegion(region);

        return View(new CompetitionDirectoryViewModel
        {
            AllRegions = allRegions,
            HasRegion = region != null,
            RegionName = region == null ? null : await region.GetDisplayNameAsync(db),
            Competitions = await query.ToListItems().ToListAsync()
        });
    }

    [HttpGet("/competitions/{slug}")]
    public async Task<IActionResult> Details(string slug, int? edition)
    {
        var c = await db.Competitions.AsNoTracking().Where(x => x.Slug == slug && x.IsActive)
            .Select(x => new
            {
                x.Id, x.Slug, x.NameAr, x.LogoPath, x.Description, x.OrganizerName, Sport = x.Sport.NameAr,
                Region = x.Area != null ? x.Area.NameAr + "، " + x.Area.Wilayat.NameAr : x.Wilayat != null ? x.Wilayat.NameAr + "، " + x.Wilayat.Governorate.NameAr : x.Governorate != null ? x.Governorate.NameAr : "سلطنة عُمان"
            }).FirstOrDefaultAsync();
        if (c == null) return NotFound();

        var editions = await db.CompetitionSeasons.AsNoTracking().Where(e => e.CompetitionId == c.Id)
            .OrderByDescending(e => e.Season.StartDate).Select(e => new { e.Id, e.Season.Name }).ToListAsync();
        var editionId = edition is { } requested && editions.Any(e => e.Id == requested) ? requested : editions.FirstOrDefault()?.Id;

        var model = new CompetitionDetailsViewModel
        {
            Id = c.Id, Slug = c.Slug, NameAr = c.NameAr, LogoPath = c.LogoPath, Description = c.Description,
            OrganizerName = c.OrganizerName, RegionText = c.Region, SportName = c.Sport,
            Editions = editions.Select(e => new SelectListItem(e.Name, e.Id.ToString(), e.Id == editionId)).ToList(),
            EditionId = editionId,
            Follow = await FollowHelper.BuildAsync(db, User.GetUserId(), FollowTargetType.Competition, c.Id, compact: false),
            CanManage = User.Identity?.IsAuthenticated == true && await db.CanManageCompetitionAsync(User, c.Id)
        };
        if (editionId is not { } eid) return View(model);

        var e = await db.CompetitionSeasons.AsNoTracking().Where(x => x.Id == eid).Select(x => new
        {
            x.Format, x.Status, x.Regulations, Season = x.Season.Name,
            Champion = x.ChampionTeam != null ? new { x.ChampionTeam.NameAr, x.ChampionTeam.Slug, x.ChampionTeam.LogoPath } : null,
            Teams = x.Teams.Count
        }).FirstAsync();
        model.SeasonName = e.Season;
        model.Format = e.Format;
        model.Status = e.Status;
        model.Regulations = e.Regulations;
        model.ChampionName = e.Champion?.NameAr;
        model.ChampionSlug = e.Champion?.Slug;
        model.ChampionLogo = e.Champion?.LogoPath;
        model.TeamsCount = e.Teams;
        model.Tables = await SportsQueries.LoadStandingsAsync(db, eid);

        var matches = await db.Matches.AsNoTracking().Visible().Where(m => m.CompetitionSeasonId == eid)
            .OrderBy(m => m.Stage != null ? m.Stage.SortOrder : 0).ThenBy(m => m.RoundNumber).ThenBy(m => m.KickoffAt)
            .ToRows().ToListAsync();
        model.MatchesTotal = matches.Count;
        model.MatchesPlayed = matches.Count(m => m.Status == MatchStatus.Finished);
        model.TotalGoals = matches.Where(m => m.Status == MatchStatus.Finished).Sum(m => m.HomeScore + m.AwayScore);
        string RoundTitle(MatchRow m) => string.Join(" · ", new[] { m.StageName, m.GroupName != null ? "المجموعة " + m.GroupName : null, m.RoundName }.Where(s => !string.IsNullOrEmpty(s)));
        model.Fixtures = matches.Where(m => m.Status != MatchStatus.Finished && m.Status != MatchStatus.Cancelled)
            .GroupBy(RoundTitle).Select(g => new RoundGroup { Title = g.Key, Matches = g.ToList() }).ToList();
        model.Results = matches.Where(m => m.Status == MatchStatus.Finished).OrderByDescending(m => m.KickoffAt)
            .GroupBy(RoundTitle).Select(g => new RoundGroup { Title = g.Key, Matches = g.ToList() }).ToList();

        var stats = db.PlayerStatistics.AsNoTracking().Where(s => s.CompetitionSeasonId == eid);
        model.TopScorers = await stats.Where(s => s.Goals > 0).OrderByDescending(s => s.Goals).ThenBy(s => s.Appearances).Take(10)
            .Select(s => new PlayerStatRow { PlayerId = s.PlayerId, Name = s.Player.FullNameAr, Photo = s.Player.PhotoPath, TeamName = s.Team.NameAr, Value = s.Goals, Secondary = s.PenaltyGoals }).ToListAsync();
        model.TopAssists = await stats.Where(s => s.Assists > 0).OrderByDescending(s => s.Assists).Take(10)
            .Select(s => new PlayerStatRow { PlayerId = s.PlayerId, Name = s.Player.FullNameAr, Photo = s.Player.PhotoPath, TeamName = s.Team.NameAr, Value = s.Assists }).ToListAsync();
        model.BestPlayers = await stats.Where(s => s.BestPlayerAwards > 0).OrderByDescending(s => s.BestPlayerAwards).ThenByDescending(s => s.Goals).Take(10)
            .Select(s => new PlayerStatRow { PlayerId = s.PlayerId, Name = s.Player.FullNameAr, Photo = s.Player.PhotoPath, TeamName = s.Team.NameAr, Value = s.BestPlayerAwards }).ToListAsync();
        model.MostAppearances = await stats.Where(s => s.Appearances > 0).OrderByDescending(s => s.Appearances).ThenByDescending(s => s.Starts).Take(10)
            .Select(s => new PlayerStatRow { PlayerId = s.PlayerId, Name = s.Player.FullNameAr, Photo = s.Player.PhotoPath, TeamName = s.Team.NameAr, Value = s.Appearances, Secondary = s.Starts }).ToListAsync();
        model.MostCards = await stats.Where(s => s.YellowCards + s.RedCards > 0).OrderByDescending(s => s.RedCards).ThenByDescending(s => s.YellowCards).Take(10)
            .Select(s => new PlayerStatRow { PlayerId = s.PlayerId, Name = s.Player.FullNameAr, Photo = s.Player.PhotoPath, TeamName = s.Team.NameAr, Value = s.YellowCards, Secondary = s.RedCards }).ToListAsync();
        model.TeamStats = await db.TeamStatistics.AsNoTracking().Where(s => s.CompetitionSeasonId == eid && s.Played > 0)
            .OrderByDescending(s => s.GoalsFor).ThenBy(s => s.GoalsAgainst)
            .Select(s => new TeamStatRow
            {
                TeamName = s.Team.NameAr, TeamSlug = s.Team.Slug, TeamLogo = s.Team.LogoPath, Played = s.Played,
                GoalsFor = s.GoalsFor, GoalsAgainst = s.GoalsAgainst, CleanSheets = s.CleanSheets
            }).ToListAsync();
        return View(model);
    }
}
