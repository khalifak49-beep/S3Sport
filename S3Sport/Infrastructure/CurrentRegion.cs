using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

/// <summary>
/// The region the visitor is currently browsing. Stored in a cookie for everyone (guests included);
/// signed-in users also persist it in UserAreas and the cookie is restored from there on login.
/// The deepest non-null level is the effective filter.
/// </summary>
public record CurrentRegion(int GovernorateId, int? WilayatId, int? AreaId, int? VillageId)
{
    public const string CookieName = "s3s_region";

    public static CurrentRegion? FromRequest(HttpRequest request) =>
        Parse(request.Cookies[CookieName]);

    public static CurrentRegion? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('-');
        if (parts.Length != 4 || !int.TryParse(parts[0], out var g) || g <= 0) return null;
        static int? Opt(string s) => int.TryParse(s, out var v) && v > 0 ? v : null;
        return new CurrentRegion(g, Opt(parts[1]), Opt(parts[2]), Opt(parts[3]));
    }

    public string Serialize() => $"{GovernorateId}-{WilayatId ?? 0}-{AreaId ?? 0}-{VillageId ?? 0}";

    public void WriteCookie(HttpResponse response) =>
        response.Cookies.Append(CookieName, Serialize(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = response.HttpContext.Request.IsHttps
        });

    public static CurrentRegion FromUserArea(UserArea ua) => new(ua.GovernorateId, ua.WilayatId, ua.AreaId, ua.VillageId);

    /// <summary>
    /// Builds a consistent region from the deepest id supplied (parents are derived from the database,
    /// so tampered or mismatched parent ids are ignored). Returns null when nothing valid was selected.
    /// </summary>
    public static async Task<CurrentRegion?> ResolveAsync(ApplicationDbContext db, int? governorateId, int? wilayatId, int? areaId, int? villageId)
    {
        if (villageId > 0)
        {
            var v = await db.Villages.AsNoTracking().Where(x => x.Id == villageId && x.IsActive)
                .Select(x => new { x.Id, x.AreaId, x.Area.WilayatId, x.Area.Wilayat.GovernorateId }).FirstOrDefaultAsync();
            if (v != null) return new CurrentRegion(v.GovernorateId, v.WilayatId, v.AreaId, v.Id);
        }
        if (areaId > 0)
        {
            var a = await db.Areas.AsNoTracking().Where(x => x.Id == areaId && x.IsActive)
                .Select(x => new { x.Id, x.WilayatId, x.Wilayat.GovernorateId }).FirstOrDefaultAsync();
            if (a != null) return new CurrentRegion(a.GovernorateId, a.WilayatId, a.Id, null);
        }
        if (wilayatId > 0)
        {
            var w = await db.Wilayats.AsNoTracking().Where(x => x.Id == wilayatId && x.IsActive)
                .Select(x => new { x.Id, x.GovernorateId }).FirstOrDefaultAsync();
            if (w != null) return new CurrentRegion(w.GovernorateId, w.Id, null, null);
        }
        if (governorateId > 0 && await db.Governorates.AnyAsync(x => x.Id == governorateId && x.IsActive))
            return new CurrentRegion(governorateId.Value, null, null, null);
        return null;
    }

    /// <summary>Arabic display text, most specific first, e.g. "الخوض، السيب، مسقط".</summary>
    public async Task<string> GetDisplayNameAsync(ApplicationDbContext db)
    {
        var names = new List<string>();
        if (VillageId.HasValue) names.Add(await db.Villages.Where(x => x.Id == VillageId).Select(x => x.NameAr).FirstOrDefaultAsync() ?? "");
        if (AreaId.HasValue) names.Add(await db.Areas.Where(x => x.Id == AreaId).Select(x => x.NameAr).FirstOrDefaultAsync() ?? "");
        if (WilayatId.HasValue) names.Add(await db.Wilayats.Where(x => x.Id == WilayatId).Select(x => x.NameAr).FirstOrDefaultAsync() ?? "");
        names.Add(await db.Governorates.Where(x => x.Id == GovernorateId).Select(x => x.NameAr).FirstOrDefaultAsync() ?? "");
        return string.Join("، ", names.Where(n => n.Length > 0));
    }
}

public static class RegionQueryExtensions
{
    /// <summary>
    /// Teams that appear in the region through any of their TeamAreas.
    /// Village selections filter at area level (teams belong to areas).
    /// </summary>
    public static IQueryable<Team> InRegion(this IQueryable<Team> teams, CurrentRegion? region)
    {
        if (region == null) return teams;
        if (region.AreaId.HasValue)
            return teams.Where(t => t.TeamAreas.Any(ta => ta.AreaId == region.AreaId));
        if (region.WilayatId.HasValue)
            return teams.Where(t => t.TeamAreas.Any(ta => ta.Area.WilayatId == region.WilayatId));
        return teams.Where(t => t.TeamAreas.Any(ta => ta.Area.Wilayat.GovernorateId == region.GovernorateId));
    }
}
