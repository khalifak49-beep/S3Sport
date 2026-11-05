using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Public teams directory and team pages (approved teams only).</summary>
public class TeamsController(ApplicationDbContext db) : Controller
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index(string? q, bool all = false, int page = 1)
    {
        var region = CurrentRegion.FromRequest(Request);
        var allRegions = all || region == null;

        var teams = db.Teams.AsNoTracking().Where(t => t.Status == TeamStatus.Approved);
        if (!allRegions) teams = teams.InRegion(region);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            teams = teams.Where(t => t.NameAr.Contains(s) || t.NameEn.Contains(s)
                || t.TeamAreas.Any(ta => ta.Area.NameAr.Contains(s) || ta.Area.Wilayat.NameAr.Contains(s)));
        }

        var cards = teams.OrderBy(t => t.NameAr).ToCards(db, UserId);
        return View(new TeamDirectoryViewModel
        {
            Results = await PagedResult<TeamCardViewModel>.CreateAsync(cards, page, 24),
            Search = q,
            AllRegions = allRegions,
            HasRegion = region != null,
            RegionName = region == null ? null : await region.GetDisplayNameAsync(db)
        });
    }

    [HttpGet("/teams/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var isAdmin = User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.Admin);
        var model = await db.Teams.AsNoTracking()
            .Where(t => t.Slug == slug && (t.Status == TeamStatus.Approved || isAdmin))
            .Select(t => new TeamDetailsViewModel
            {
                Id = t.Id, Slug = t.Slug, NameAr = t.NameAr, NameEn = t.NameEn, ShortName = t.ShortName,
                LogoPath = t.LogoPath, FoundedYear = t.FoundedYear, Description = t.Description,
                ContactEmail = t.ContactEmail, SocialUrl = t.SocialUrl, PrimaryColor = t.PrimaryColor,
                SportName = t.Sport.NameAr,
                PrimaryAreaText = t.TeamAreas.Where(ta => ta.IsPrimary)
                    .Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr + "، " + ta.Area.Wilayat.Governorate.NameAr).FirstOrDefault(),
                OtherAreas = t.TeamAreas.Where(ta => !ta.IsPrimary).Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr).ToList(),
                Players = t.TeamPlayers.Where(tp => tp.IsActive).OrderBy(tp => tp.ShirtNumber)
                    .Select(tp => new TeamPlayerRow
                    {
                        PlayerId = tp.PlayerId, FullNameAr = tp.Player.FullNameAr, PhotoPath = tp.Player.PhotoPath, ShirtNumber = tp.ShirtNumber,
                        Position = tp.Position != null ? tp.Position.NameAr : null, IsCaptain = tp.IsCaptain
                    }).ToList(),
                Achievements = t.Achievements.OrderByDescending(a => a.Year)
                    .Select(a => new AchievementRow { TitleAr = a.TitleAr, Year = a.Year, Description = a.Description }).ToList()
            })
            .FirstOrDefaultAsync();
        if (model == null) return NotFound();

        model.Follow = await FollowHelper.BuildAsync(db, UserId, FollowTargetType.Team, model.Id, compact: false);
        model.CanAdminEdit = isAdmin;
        model.CanManage = User.Identity?.IsAuthenticated == true && await db.CanManageTeamAsync(User, model.Id);

        var teamId = model.Id;
        var teamMatches = db.Matches.AsNoTracking().Visible().Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);
        model.UpcomingMatches = await teamMatches
            .Where(m => m.Status == MatchStatus.Scheduled || m.Status == MatchStatus.Postponed || m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime)
            .OrderByDescending(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime).ThenBy(m => m.KickoffAt)
            .Take(5).ToRows().ToListAsync();
        model.RecentMatches = await teamMatches.Where(m => m.Status == MatchStatus.Finished).OrderByDescending(m => m.KickoffAt).Take(8).ToRows().ToListAsync();
        model.Competitions = await db.Competitions.AsNoTracking()
            .Where(c => c.IsActive && c.Seasons.Any(s => s.Teams.Any(t => t.TeamId == teamId))).ToListItems().ToListAsync();
        model.News = await db.News.AsNoTracking().Where(n => n.IsPublished && n.TeamId == teamId)
            .OrderByDescending(n => n.PublishedAt).Take(4).ToListItems().ToListAsync();

        var totals = await db.TeamStatistics.AsNoTracking().Where(s => s.TeamId == teamId)
            .GroupBy(s => s.TeamId)
            .Select(g => new { Played = g.Sum(s => s.Played), Won = g.Sum(s => s.Won), Drawn = g.Sum(s => s.Drawn), Lost = g.Sum(s => s.Lost),
                GoalsFor = g.Sum(s => s.GoalsFor), GoalsAgainst = g.Sum(s => s.GoalsAgainst), CleanSheets = g.Sum(s => s.CleanSheets) })
            .FirstOrDefaultAsync();
        if (totals != null)
        {
            model.Totals = new TeamStatRow { Played = totals.Played, GoalsFor = totals.GoalsFor, GoalsAgainst = totals.GoalsAgainst, CleanSheets = totals.CleanSheets };
            model.Won = totals.Won; model.Drawn = totals.Drawn; model.Lost = totals.Lost;
        }
        return View(model);
    }
}
