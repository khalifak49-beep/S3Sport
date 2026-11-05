using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>Sports and their playing positions. New sports are data, not code (no schema change needed).</summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class SportsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.Sports.AsNoTracking().Include(s => s.Positions).OrderBy(s => s.SortOrder).ThenBy(s => s.NameAr).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new SportFormViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await db.Sports.AsNoTracking().Include(x => x.Positions).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        return View(new SportFormViewModel
        {
            Id = s.Id, NameAr = s.NameAr, NameEn = s.NameEn, Code = s.Code, IconClass = s.IconClass,
            PlayersOnField = s.PlayersOnField, PeriodsCount = s.PeriodsCount, PeriodDurationMinutes = s.PeriodDurationMinutes,
            PointsForWin = s.PointsForWin, PointsForDraw = s.PointsForDraw, PointsForLoss = s.PointsForLoss, IsActive = s.IsActive,
            Positions = s.Positions.OrderBy(p => p.SortOrder).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(SportFormViewModel model)
    {
        if (await db.Sports.AnyAsync(s => s.Id != model.Id && (s.Code == model.Code || s.NameAr == model.NameAr)))
            ModelState.AddModelError(nameof(model.Code), "يوجد رياضة بنفس الاسم أو الرمز.");
        if (!ModelState.IsValid)
        {
            model.Positions = await db.SportPositions.Where(p => p.SportId == model.Id).OrderBy(p => p.SortOrder).ToListAsync();
            return View("Edit", model);
        }

        var sport = model.Id == 0 ? new Sport() : await db.Sports.FindAsync(model.Id);
        if (sport == null) return NotFound();
        if (model.Id == 0)
        {
            sport.SortOrder = (await db.Sports.MaxAsync(s => (int?)s.SortOrder) ?? 0) + 1;
            db.Sports.Add(sport);
        }
        sport.NameAr = model.NameAr.Trim();
        sport.NameEn = model.NameEn.Trim();
        sport.Code = model.Code.Trim();
        sport.IconClass = model.IconClass?.Trim();
        sport.PlayersOnField = model.PlayersOnField;
        sport.PeriodsCount = model.PeriodsCount;
        sport.PeriodDurationMinutes = model.PeriodDurationMinutes;
        sport.PointsForWin = model.PointsForWin;
        sport.PointsForDraw = model.PointsForDraw;
        sport.PointsForLoss = model.PointsForLoss;
        sport.IsActive = model.IsActive;
        await db.SaveChangesAsync();

        db.AddAudit(HttpContext, model.Id == 0 ? "إضافة رياضة" : "تعديل رياضة", nameof(Sport), sport.Id, sport.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ الرياضة.";
        return RedirectToAction(nameof(Edit), new { id = sport.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AddPosition(int id, string nameAr, string nameEn, string code)
    {
        if (!await db.Sports.AnyAsync(s => s.Id == id)) return NotFound();
        nameAr = nameAr?.Trim() ?? ""; nameEn = nameEn?.Trim() ?? ""; code = code?.Trim().ToUpperInvariant() ?? "";
        if (nameAr.Length is 0 or > 60 || nameEn.Length is 0 or > 60 || code.Length is 0 or > 10)
            TempData["Error"] = "أدخل اسم المركز بالعربية والإنجليزية ورمزاً قصيراً.";
        else if (await db.SportPositions.AnyAsync(p => p.SportId == id && p.Code == code))
            TempData["Error"] = "رمز المركز مستخدم في هذه الرياضة.";
        else
        {
            var order = await db.SportPositions.Where(p => p.SportId == id).MaxAsync(p => (int?)p.SortOrder) ?? 0;
            db.SportPositions.Add(new SportPosition { SportId = id, NameAr = nameAr, NameEn = nameEn, Code = code, SortOrder = order + 1 });
            db.AddAudit(HttpContext, "إضافة مركز لعب", nameof(Sport), id, nameAr);
            await db.SaveChangesAsync();
            TempData["Success"] = "تمت إضافة المركز.";
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeletePosition(int id, int positionId)
    {
        var p = await db.SportPositions.FirstOrDefaultAsync(x => x.Id == positionId && x.SportId == id);
        if (p == null) return NotFound();
        db.SportPositions.Remove(p);
        db.AddAudit(HttpContext, "حذف مركز لعب", nameof(Sport), id, p.NameAr);
        try
        {
            await db.SaveChangesAsync();
            TempData["Success"] = "تم حذف المركز.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "لا يمكن حذف مركز مستخدم للاعبين.";
        }
        return RedirectToAction(nameof(Edit), new { id });
    }
}
