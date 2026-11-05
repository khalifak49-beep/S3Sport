using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>All current league / group tables for the visitor's region (or all of Oman).</summary>
public class StandingsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(bool all = false)
    {
        var region = CurrentRegion.FromRequest(Request);
        var allRegions = all || region == null;
        var competitions = db.Competitions.AsNoTracking().Where(c => c.IsActive);
        if (!allRegions) competitions = competitions.InRegion(region);

        // Latest edition of each competition that has a table.
        var editions = await competitions
            .Select(c => c.Seasons.Where(s => s.Standings.Any()).OrderByDescending(s => s.Season.StartDate)
                .Select(s => new { s.Id, Competition = c.NameAr, c.Slug, c.LogoPath, Season = s.Season.Name, s.Status })
                .FirstOrDefault())
            .Where(x => x != null)
            .ToListAsync();

        var model = new StandingsPageViewModel
        {
            AllRegions = allRegions,
            HasRegion = region != null,
            RegionName = region == null ? null : await region.GetDisplayNameAsync(db)
        };
        foreach (var e in editions.OrderBy(e => e!.Status == CompetitionSeasonStatus.Completed).ThenBy(e => e!.Competition))
        {
            model.Blocks.Add(new StandingsBlock
            {
                CompetitionName = e!.Competition, CompetitionSlug = e.Slug, CompetitionLogo = e.LogoPath, SeasonName = e.Season,
                Tables = await SportsQueries.LoadStandingsAsync(db, e.Id)
            });
        }
        return View(model);
    }
}
