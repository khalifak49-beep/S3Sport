using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

public record SponsorCard(int Id, string Name, string? Tagline, string Logo, bool HasLink);

/// <summary>
/// The sponsor strip. Picks active sponsors for the place it is shown:
/// a match (its competition, then the home team's wilayat / governorate), a competition, or the visitor's region.
/// The most specific sponsors come first, then the national ones. Each render counts one view per sponsor shown.
/// </summary>
public class SponsorsViewComponent(ApplicationDbContext db) : ViewComponent
{
    public const int MaxShown = 4;

    public async Task<IViewComponentResult> InvokeAsync(int? matchId = null, int? competitionId = null, bool region = false, string? title = null)
    {
        int? governorateId = null, wilayatId = null;
        if (matchId is { } mid)
        {
            var m = await db.Matches.AsNoTracking().Where(x => x.Id == mid).Select(x => new
            {
                CompetitionId = x.CompetitionSeason != null ? (int?)x.CompetitionSeason.CompetitionId : null,
                Area = x.HomeTeam.TeamAreas.OrderByDescending(a => a.IsPrimary).Select(a => new { a.Area.WilayatId, a.Area.Wilayat.GovernorateId }).FirstOrDefault()
            }).FirstOrDefaultAsync();
            if (m == null) return Content(string.Empty);
            competitionId = m.CompetitionId;
            wilayatId = m.Area?.WilayatId;
            governorateId = m.Area?.GovernorateId;
        }
        else if (region && CurrentRegion.FromRequest(HttpContext.Request) is { } r)
        {
            governorateId = r.GovernorateId;
            wilayatId = r.WilayatId;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.ToMuscat());
        var rows = await db.Sponsors.AsNoTracking()
            .Where(s => s.IsActive && (s.StartsOn == null || s.StartsOn <= today) && (s.EndsOn == null || s.EndsOn >= today))
            .Where(s => (s.Scope == SponsorScope.Competition && competitionId != null && s.CompetitionId == competitionId)
                     || (s.Scope == SponsorScope.Wilayat && wilayatId != null && s.WilayatId == wilayatId)
                     || (s.Scope == SponsorScope.Governorate && governorateId != null && s.GovernorateId == governorateId)
                     || s.Scope == SponsorScope.Country)
            .OrderByDescending(s => s.Scope == SponsorScope.Competition).ThenByDescending(s => s.Scope == SponsorScope.Wilayat)
            .ThenByDescending(s => s.Scope == SponsorScope.Governorate).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Take(MaxShown)
            .Select(s => new SponsorCard(s.Id, s.NameAr, s.Tagline, s.LogoPath, s.WebsiteUrl != null || s.Phone != null))
            .ToListAsync();
        if (rows.Count == 0) return Content(string.Empty);

        if (!IsBot(HttpContext.Request.Headers.UserAgent.ToString()))
        {
            var ids = rows.Select(r => r.Id).ToList();
            await db.Sponsors.Where(s => ids.Contains(s.Id)).ExecuteUpdateAsync(u => u.SetProperty(s => s.Views, s => s.Views + 1));
        }

        ViewData["SponsorTitle"] = title ?? (competitionId != null && matchId == null ? "رعاة البطولة" : "برعاية");
        return View(rows);
    }

    public static bool IsBot(string userAgent) =>
        string.IsNullOrEmpty(userAgent) || userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("spider", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("crawl", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("HeadlessChrome", StringComparison.OrdinalIgnoreCase) || userAgent.StartsWith("curl", StringComparison.OrdinalIgnoreCase);
}
