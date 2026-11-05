using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var region = CurrentRegion.FromRequest(Request);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = new HomeIndexViewModel { HasRegion = region != null };
        var approved = db.Teams.AsNoTracking().Where(t => t.Status == TeamStatus.Approved);

        if (region != null)
        {
            model.RegionName = await region.GetDisplayNameAsync(db);
            var regionTeams = approved.InRegion(region);
            model.TotalTeamsInRegion = await regionTeams.CountAsync();
            model.Teams = await regionTeams.OrderBy(t => t.NameAr).Take(12).ToCards(db, userId).ToListAsync();
        }

        // Matches: the region's, falling back to all of Oman when the region has none in this window.
        var now = DateTime.UtcNow;
        var todayStart = DateOnly.FromDateTime(now.ToMuscat()).ToDateTime(TimeOnly.MinValue).FromMuscat();
        var visible = db.Matches.AsNoTracking().Visible();
        var scoped = region == null ? visible : visible.InRegion(region);
        if (region != null && !await scoped.AnyAsync(m => m.KickoffAt >= now.AddDays(-14) && m.KickoffAt < now.AddDays(14)))
        {
            scoped = visible;
            model.MatchesAreNational = true;
        }
        model.LiveMatches = await scoped.Where(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime)
            .OrderBy(m => m.KickoffAt).ToRows().ToListAsync();
        model.TodayMatches = await scoped.Where(m => m.KickoffAt >= todayStart && m.KickoffAt < todayStart.AddDays(1)
                && m.Status != MatchStatus.Live && m.Status != MatchStatus.HalfTime)
            .OrderBy(m => m.KickoffAt).ToRows().ToListAsync();
        model.UpcomingMatches = await scoped.Where(m => m.KickoffAt >= todayStart.AddDays(1) && m.Status == MatchStatus.Scheduled)
            .OrderBy(m => m.KickoffAt).Take(6).ToRows().ToListAsync();
        model.LatestResults = await scoped.Where(m => m.Status == MatchStatus.Finished && m.KickoffAt < todayStart)
            .OrderByDescending(m => m.KickoffAt).Take(6).ToRows().ToListAsync();
        model.ActiveCompetitions = await db.Competitions.AsNoTracking().InRegion(region)
            .Where(c => c.IsActive && c.Seasons.Any(s => s.Status == CompetitionSeasonStatus.InProgress || s.Status == CompetitionSeasonStatus.Registration))
            .ToListItems().Take(6).ToListAsync();
        model.LatestNews = await db.News.AsNoTracking().Where(n => n.IsPublished)
            .OrderByDescending(n => n.PublishedAt).Take(4).ToListItems().ToListAsync();

        if (userId != null)
        {
            model.FollowedTeams = await approved
                .Where(t => db.UserFollows.Any(f => f.TeamId == t.Id && f.UserId == userId))
                .OrderBy(t => t.NameAr).Take(8).ToCards(db, userId).ToListAsync();
        }

        // Most followed in the region; if the region has none yet, show the national list instead.
        model.MostFollowed = await MostFollowedAsync(approved.InRegion(region), userId);
        if (model.MostFollowed.Count == 0 && region != null)
        {
            model.MostFollowed = await MostFollowedAsync(approved, userId);
            model.MostFollowedIsNational = true;
        }
        else
        {
            model.MostFollowedIsNational = region == null;
        }
        return View(model);
    }

    private Task<List<TeamCardViewModel>> MostFollowedAsync(IQueryable<Team> teams, string? userId) =>
        teams.Where(t => db.UserFollows.Any(f => f.TeamId == t.Id))
            .OrderByDescending(t => db.UserFollows.Count(f => f.TeamId == t.Id)).ThenBy(t => t.NameAr)
            .Take(6).ToCards(db, userId).ToListAsync();

    [Route("/Home/StatusCode/{code:int}")]
    public new IActionResult StatusCode(int code)
    {
        Response.StatusCode = code;
        ViewData["Code"] = code;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
