using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Region selection for visitors and users, plus read-only JSON lookups used by cascading dropdowns.</summary>
public class RegionController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        var current = CurrentRegion.FromRequest(Request);
        var model = new RegionSelectViewModel
        {
            ReturnUrl = returnUrl,
            GovernorateId = current?.GovernorateId ?? 0,
            WilayatId = current?.WilayatId,
            AreaId = current?.AreaId,
            VillageId = current?.VillageId
        };
        await FillAsync(model, current);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index(RegionSelectViewModel model)
    {
        var region = ModelState.IsValid
            ? await CurrentRegion.ResolveAsync(db, model.GovernorateId, model.WilayatId, model.AreaId, model.VillageId)
            : null;
        if (region == null)
        {
            ModelState.AddModelError(string.Empty, "يرجى اختيار منطقة صالحة.");
            await FillAsync(model, CurrentRegion.FromRequest(Request));
            return View(model);
        }

        region.WriteCookie(Response);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
            await SaveUserRegionAsync(userId, region, model.MakeDefault);

        TempData["Success"] = $"تم اختيار المنطقة: {await region.GetDisplayNameAsync(db)}";
        return RedirectToReturnUrl(model.ReturnUrl);
    }

    /// <summary>Switch the current region to one of the user's saved regions.</summary>
    [HttpPost, Authorize]
    public async Task<IActionResult> Use(int id, bool makeDefault = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ua = await db.UserAreas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (ua == null) return NotFound();

        var region = CurrentRegion.FromUserArea(ua);
        region.WriteCookie(Response);
        if (makeDefault) await SaveUserRegionAsync(userId, region, true);

        TempData["Success"] = makeDefault ? "تم تعيين المنطقة الافتراضية." : "تم تغيير المنطقة الحالية.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Remove(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ua = await db.UserAreas.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (ua == null) return NotFound();

        db.UserAreas.Remove(ua);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف المنطقة من قائمتك.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- JSON lookups (public, read-only) ----------

    [HttpGet]
    public async Task<IActionResult> Wilayats(int governorateId) =>
        Json(await db.Wilayats.AsNoTracking()
            .Where(x => x.GovernorateId == governorateId && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
            .Select(x => new RegionOption(x.Id, x.NameAr)).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Areas(int wilayatId) =>
        Json(await db.Areas.AsNoTracking()
            .Where(x => x.WilayatId == wilayatId && x.IsActive)
            .OrderBy(x => x.NameAr)
            .Select(x => new RegionOption(x.Id, x.NameAr)).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Villages(int areaId) =>
        Json(await db.Villages.AsNoTracking()
            .Where(x => x.AreaId == areaId && x.IsActive)
            .OrderBy(x => x.NameAr)
            .Select(x => new RegionOption(x.Id, x.NameAr)).ToListAsync());

    /// <summary>Areas of a governorate labelled with their wilayat (for "additional areas" pickers).</summary>
    [HttpGet]
    public async Task<IActionResult> GovernorateAreas(int governorateId) =>
        Json(await db.Areas.AsNoTracking()
            .Where(x => x.Wilayat.GovernorateId == governorateId && x.IsActive)
            .OrderBy(x => x.Wilayat.SortOrder).ThenBy(x => x.NameAr)
            .Select(x => new RegionOption(x.Id, x.NameAr + " (" + x.Wilayat.NameAr + ")")).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        q = q?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length < 2 || q.Length > 50) return Json(Array.Empty<RegionSearchResult>());

        var results = new List<RegionSearchResult>();
        results.AddRange(await db.Governorates.AsNoTracking()
            .Where(x => x.IsActive && (x.NameAr.Contains(q) || x.NameEn.Contains(q)))
            .Select(x => new RegionSearchResult("محافظة " + x.NameAr, "محافظة", x.Id, null, null, null))
            .Take(5).ToListAsync());
        results.AddRange(await db.Wilayats.AsNoTracking()
            .Where(x => x.IsActive && (x.NameAr.Contains(q) || x.NameEn.Contains(q)))
            .Select(x => new RegionSearchResult(x.NameAr + "، " + x.Governorate.NameAr, "ولاية", x.GovernorateId, x.Id, null, null))
            .Take(10).ToListAsync());
        results.AddRange(await db.Areas.AsNoTracking()
            .Where(x => x.IsActive && (x.NameAr.Contains(q) || x.NameEn.Contains(q)))
            .Select(x => new RegionSearchResult(x.NameAr + "، " + x.Wilayat.NameAr, "منطقة", x.Wilayat.GovernorateId, x.WilayatId, x.Id, null))
            .Take(10).ToListAsync());
        results.AddRange(await db.Villages.AsNoTracking()
            .Where(x => x.IsActive && (x.NameAr.Contains(q) || x.NameEn.Contains(q)))
            .Select(x => new RegionSearchResult(x.NameAr + "، " + x.Area.NameAr + "، " + x.Area.Wilayat.NameAr, "قرية",
                x.Area.Wilayat.GovernorateId, x.Area.WilayatId, x.AreaId, x.Id))
            .Take(10).ToListAsync());

        return Json(results);
    }

    // ---------- helpers ----------

    private async Task FillAsync(RegionSelectViewModel model, CurrentRegion? current)
    {
        model.Governorates = await db.Governorates.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new SelectListItem(x.NameAr, x.Id.ToString())).ToListAsync();
        model.CurrentRegionName = current == null ? null : await current.GetDisplayNameAsync(db);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var saved = await db.UserAreas.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAt).ToListAsync();
        foreach (var ua in saved)
        {
            var region = CurrentRegion.FromUserArea(ua);
            model.SavedRegions.Add(new SavedRegionItem
            {
                Id = ua.Id,
                DisplayName = await region.GetDisplayNameAsync(db),
                IsDefault = ua.IsDefault,
                IsCurrent = region == current
            });
        }
    }

    /// <summary>Adds the region to the user's list (if new) and optionally makes it the only default.</summary>
    private async Task SaveUserRegionAsync(string userId, CurrentRegion region, bool makeDefault)
    {
        await db.InTransactionAsync(async () =>
        {
            var existing = await db.UserAreas.FirstOrDefaultAsync(x => x.UserId == userId
                && x.GovernorateId == region.GovernorateId && x.WilayatId == region.WilayatId
                && x.AreaId == region.AreaId && x.VillageId == region.VillageId);

            var hasDefault = await db.UserAreas.AnyAsync(x => x.UserId == userId && x.IsDefault);
            makeDefault |= !hasDefault; // the first saved region always becomes the default

            if (makeDefault)
            {
                // Clear the old default first: the filtered unique index allows only one default per user.
                await db.UserAreas.Where(x => x.UserId == userId && x.IsDefault && x.Id != (existing != null ? existing.Id : 0))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));
            }

            if (existing == null)
            {
                db.UserAreas.Add(new UserArea
                {
                    UserId = userId, GovernorateId = region.GovernorateId, WilayatId = region.WilayatId,
                    AreaId = region.AreaId, VillageId = region.VillageId, IsDefault = makeDefault
                });
            }
            else if (makeDefault)
            {
                existing.IsDefault = true;
            }
            await db.SaveChangesAsync();
        });
    }

    private IActionResult RedirectToReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
}
