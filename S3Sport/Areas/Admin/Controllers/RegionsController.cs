using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>
/// Drill-down management of governorates → wilayats → areas → villages.
/// One controller handles all four levels because they share the same shape (names, active flag, parent).
/// </summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class RegionsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(RegionLevel level = RegionLevel.Governorate, int? parentId = null, string? search = null)
    {
        if (level != RegionLevel.Governorate && parentId == null) return RedirectToAction(nameof(Index));

        var s = search?.Trim();
        var model = new RegionAdminListViewModel { Level = level, ParentId = parentId, Search = search };
        model.Items = level switch
        {
            RegionLevel.Governorate => await db.Governorates.AsNoTracking()
                .Where(x => s == null || x.NameAr.Contains(s) || x.NameEn.Contains(s))
                .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
                .Select(x => new RegionAdminRow { Id = x.Id, NameAr = x.NameAr, NameEn = x.NameEn, SortOrder = x.SortOrder, IsActive = x.IsActive, ChildCount = x.Wilayats.Count })
                .ToListAsync(),
            RegionLevel.Wilayat => await db.Wilayats.AsNoTracking()
                .Where(x => x.GovernorateId == parentId && (s == null || x.NameAr.Contains(s) || x.NameEn.Contains(s)))
                .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
                .Select(x => new RegionAdminRow { Id = x.Id, NameAr = x.NameAr, NameEn = x.NameEn, SortOrder = x.SortOrder, IsActive = x.IsActive, ChildCount = x.Areas.Count })
                .ToListAsync(),
            RegionLevel.Area => await db.Areas.AsNoTracking()
                .Where(x => x.WilayatId == parentId && (s == null || x.NameAr.Contains(s) || x.NameEn.Contains(s)))
                .OrderBy(x => x.NameAr)
                .Select(x => new RegionAdminRow { Id = x.Id, NameAr = x.NameAr, NameEn = x.NameEn, IsActive = x.IsActive, ChildCount = x.Villages.Count })
                .ToListAsync(),
            _ => await db.Villages.AsNoTracking()
                .Where(x => x.AreaId == parentId && (s == null || x.NameAr.Contains(s) || x.NameEn.Contains(s)))
                .OrderBy(x => x.NameAr)
                .Select(x => new RegionAdminRow { Id = x.Id, NameAr = x.NameAr, NameEn = x.NameEn, IsActive = x.IsActive })
                .ToListAsync()
        };
        model.Breadcrumbs = await BuildBreadcrumbsAsync(level, parentId);
        if (level != RegionLevel.Governorate && model.Breadcrumbs.Count == 0) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(RegionLevel level, int? parentId)
    {
        var model = new RegionFormViewModel { Level = level, ParentId = parentId };
        if (!await FillParentAsync(model)) return NotFound();
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(RegionLevel level, int id)
    {
        RegionFormViewModel? model = level switch
        {
            RegionLevel.Governorate => await db.Governorates.Where(x => x.Id == id).Select(x => new RegionFormViewModel
                { Level = level, Id = x.Id, NameAr = x.NameAr, NameEn = x.NameEn, SortOrder = x.SortOrder, IsActive = x.IsActive }).FirstOrDefaultAsync(),
            RegionLevel.Wilayat => await db.Wilayats.Where(x => x.Id == id).Select(x => new RegionFormViewModel
                { Level = level, Id = x.Id, ParentId = x.GovernorateId, NameAr = x.NameAr, NameEn = x.NameEn, SortOrder = x.SortOrder, IsActive = x.IsActive }).FirstOrDefaultAsync(),
            RegionLevel.Area => await db.Areas.Where(x => x.Id == id).Select(x => new RegionFormViewModel
                { Level = level, Id = x.Id, ParentId = x.WilayatId, NameAr = x.NameAr, NameEn = x.NameEn, IsActive = x.IsActive }).FirstOrDefaultAsync(),
            _ => await db.Villages.Where(x => x.Id == id).Select(x => new RegionFormViewModel
                { Level = level, Id = x.Id, ParentId = x.AreaId, NameAr = x.NameAr, NameEn = x.NameEn, IsActive = x.IsActive }).FirstOrDefaultAsync()
        };
        if (model == null) return NotFound();
        await FillParentAsync(model);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Save(RegionFormViewModel model)
    {
        if (!await FillParentAsync(model)) return NotFound();
        model.NameAr = model.NameAr?.Trim() ?? "";
        model.NameEn = model.NameEn?.Trim() ?? "";

        if (ModelState.IsValid && await NameExistsAsync(model))
            ModelState.AddModelError(nameof(model.NameAr), "يوجد اسم مطابق في نفس المستوى.");
        if (!ModelState.IsValid) return View("Form", model);

        BaseEntity entity = model.Level switch
        {
            RegionLevel.Governorate => await Upsert(db.Governorates, model.Id, () => new Governorate(), x =>
                { x.NameAr = model.NameAr; x.NameEn = model.NameEn; x.SortOrder = model.SortOrder; x.IsActive = model.IsActive; }),
            RegionLevel.Wilayat => await Upsert(db.Wilayats, model.Id, () => new Wilayat { GovernorateId = model.ParentId!.Value }, x =>
                { x.NameAr = model.NameAr; x.NameEn = model.NameEn; x.SortOrder = model.SortOrder; x.IsActive = model.IsActive; }),
            RegionLevel.Area => await Upsert(db.Areas, model.Id, () => new Area { WilayatId = model.ParentId!.Value }, x =>
                { x.NameAr = model.NameAr; x.NameEn = model.NameEn; x.IsActive = model.IsActive; }),
            _ => await Upsert(db.Villages, model.Id, () => new Village { AreaId = model.ParentId!.Value }, x =>
                { x.NameAr = model.NameAr; x.NameEn = model.NameEn; x.IsActive = model.IsActive; })
        };
        await db.SaveChangesAsync();

        db.AddAudit(HttpContext, model.Id == 0 ? "إضافة منطقة" : "تعديل منطقة", model.Level.ToString(), entity.Id, model.NameAr);
        await db.SaveChangesAsync();

        TempData["Success"] = model.Id == 0 ? $"تمت إضافة «{model.NameAr}»." : $"تم حفظ «{model.NameAr}».";
        return RedirectToAction(nameof(Index), new { level = model.Level, parentId = model.ParentId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(RegionLevel level, int id)
    {
        object? entity = level switch
        {
            RegionLevel.Governorate => await db.Governorates.FindAsync(id),
            RegionLevel.Wilayat => await db.Wilayats.FindAsync(id),
            RegionLevel.Area => await db.Areas.FindAsync(id),
            _ => await db.Villages.FindAsync(id)
        };
        if (entity == null) return NotFound();

        int? parentId = entity switch { Wilayat w => w.GovernorateId, Area a => a.WilayatId, Village v => v.AreaId, _ => null };
        var name = entity switch { Governorate g => g.NameAr, Wilayat w => w.NameAr, Area a => a.NameAr, Village v => v.NameAr, _ => "" };

        db.Remove(entity);
        db.AddAudit(HttpContext, "حذف منطقة", level.ToString(), id, name);
        try
        {
            await db.SaveChangesAsync();
            TempData["Success"] = $"تم حذف «{name}».";
        }
        catch (DbUpdateException)
        {
            // FKs are RESTRICT: anything still referencing this region blocks the delete.
            TempData["Error"] = $"لا يمكن حذف «{name}» لارتباطه بمناطق فرعية أو فرق أو مستخدمين. يمكنك تعطيله بدلاً من ذلك.";
        }
        return RedirectToAction(nameof(Index), new { level, parentId });
    }

    // ---------- helpers ----------

    private async Task<T> Upsert<T>(DbSet<T> set, int id, Func<T> create, Action<T> apply) where T : BaseEntity
    {
        var entity = id == 0 ? null : await set.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            entity = create();
            set.Add(entity);
        }
        apply(entity);
        return entity;
    }

    private Task<bool> NameExistsAsync(RegionFormViewModel m) => m.Level switch
    {
        RegionLevel.Governorate => db.Governorates.AnyAsync(x => x.Id != m.Id && (x.NameAr == m.NameAr || x.NameEn == m.NameEn)),
        RegionLevel.Wilayat => db.Wilayats.AnyAsync(x => x.Id != m.Id && x.GovernorateId == m.ParentId && x.NameAr == m.NameAr),
        RegionLevel.Area => db.Areas.AnyAsync(x => x.Id != m.Id && x.WilayatId == m.ParentId && x.NameAr == m.NameAr),
        _ => db.Villages.AnyAsync(x => x.Id != m.Id && x.AreaId == m.ParentId && x.NameAr == m.NameAr)
    };

    /// <summary>Loads the parent's name; returns false when a required parent does not exist.</summary>
    private async Task<bool> FillParentAsync(RegionFormViewModel model)
    {
        model.ParentName = model.Level switch
        {
            RegionLevel.Governorate => null,
            RegionLevel.Wilayat => await db.Governorates.Where(x => x.Id == model.ParentId).Select(x => x.NameAr).FirstOrDefaultAsync(),
            RegionLevel.Area => await db.Wilayats.Where(x => x.Id == model.ParentId).Select(x => x.NameAr).FirstOrDefaultAsync(),
            _ => await db.Areas.Where(x => x.Id == model.ParentId).Select(x => x.NameAr).FirstOrDefaultAsync()
        };
        if (model.Level == RegionLevel.Governorate) model.ParentId = null;
        return model.Level == RegionLevel.Governorate || model.ParentName != null;
    }

    private async Task<IList<(RegionLevel, int, string)>> BuildBreadcrumbsAsync(RegionLevel level, int? parentId)
    {
        var crumbs = new List<(RegionLevel, int, string)>();
        switch (level)
        {
            case RegionLevel.Wilayat:
                var g = await db.Governorates.Where(x => x.Id == parentId).Select(x => new { x.Id, x.NameAr }).FirstOrDefaultAsync();
                if (g != null) crumbs.Add((RegionLevel.Governorate, g.Id, g.NameAr));
                break;
            case RegionLevel.Area:
                var w = await db.Wilayats.Where(x => x.Id == parentId)
                    .Select(x => new { x.Id, x.NameAr, GId = x.GovernorateId, GName = x.Governorate.NameAr }).FirstOrDefaultAsync();
                if (w != null) crumbs.AddRange([(RegionLevel.Governorate, w.GId, w.GName), (RegionLevel.Wilayat, w.Id, w.NameAr)]);
                break;
            case RegionLevel.Village:
                var a = await db.Areas.Where(x => x.Id == parentId)
                    .Select(x => new { x.Id, x.NameAr, WId = x.WilayatId, WName = x.Wilayat.NameAr, GId = x.Wilayat.GovernorateId, GName = x.Wilayat.Governorate.NameAr })
                    .FirstOrDefaultAsync();
                if (a != null) crumbs.AddRange([(RegionLevel.Governorate, a.GId, a.GName), (RegionLevel.Wilayat, a.WId, a.WName), (RegionLevel.Area, a.Id, a.NameAr)]);
                break;
        }
        return crumbs;
    }
}
