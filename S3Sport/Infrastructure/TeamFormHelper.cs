using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

/// <summary>Shared validation/list-filling for the user team-application form and the admin team form.</summary>
public static class TeamFormHelper
{
    public static async Task FillListsAsync(ApplicationDbContext db, TeamFormBase model)
    {
        model.Governorates = await db.Governorates.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new SelectListItem(x.NameAr, x.Id.ToString())).ToListAsync();
    }

    /// <summary>
    /// Checks the governorate → wilayat → area → village chain is consistent and active.
    /// When the form allows it and no area is selected, a typed area name is accepted instead;
    /// if that name already exists in the wilayat the existing area is selected automatically.
    /// Returns the cleaned proposed area name (null when an existing area is used).
    /// </summary>
    public static async Task<string?> ValidateRegionAsync(ApplicationDbContext db, TeamFormBase model, ModelStateDictionary modelState)
    {
        if (model.AreaId <= 0) model.AreaId = null;
        if (model.VillageId <= 0) model.VillageId = null;
        var proposed = model.AllowsProposedArea && model.AreaId == null ? model.ProposedArea?.Trim() : null;
        if (string.IsNullOrEmpty(proposed)) proposed = null;

        if (model.AreaId == null && proposed == null)
        {
            modelState.AddModelError(nameof(model.AreaId), model.AllowsProposedArea
                ? "يرجى اختيار المنطقة، أو كتابة اسمها إذا لم تجدها في القائمة."
                : "يرجى اختيار المنطقة.");
            return null;
        }

        if (proposed != null)
        {
            var wilayatValid = await db.Wilayats.AnyAsync(w => w.Id == model.WilayatId && w.GovernorateId == model.GovernorateId && w.IsActive);
            if (!wilayatValid)
            {
                modelState.AddModelError(nameof(model.WilayatId), "الولاية المختارة لا تتبع المحافظة المحددة.");
                return null;
            }
            var existingId = await db.Areas.Where(a => a.WilayatId == model.WilayatId && a.NameAr == proposed && a.IsActive)
                .Select(a => (int?)a.Id).FirstOrDefaultAsync();
            if (existingId == null)
            {
                model.VillageId = null; // villages belong to registered areas only
                return proposed;
            }
            model.AreaId = existingId; // the typed name already exists: use it
        }

        var area = await db.Areas.AsNoTracking()
            .Where(a => a.Id == model.AreaId && a.IsActive)
            .Select(a => new { a.WilayatId, a.Wilayat.GovernorateId })
            .FirstOrDefaultAsync();
        if (area == null || area.WilayatId != model.WilayatId || area.GovernorateId != model.GovernorateId)
            modelState.AddModelError(nameof(model.AreaId), "المنطقة المختارة لا تتبع الولاية أو المحافظة المحددة.");

        if (model.VillageId != null && !await db.Villages.AnyAsync(v => v.Id == model.VillageId && v.AreaId == model.AreaId && v.IsActive))
            modelState.AddModelError(nameof(model.VillageId), "القرية المختارة لا تتبع المنطقة المحددة.");

        return null;
    }

    /// <summary>Validates "additional areas" (separate so the primary-area rules above stay readable).</summary>
    public static async Task ValidateAdditionalAreasAsync(ApplicationDbContext db, TeamFormBase model, ModelStateDictionary modelState)
    {
        model.AdditionalAreaIds =model.AdditionalAreaIds.Where(id => id != model.AreaId).Distinct().ToList();
        if (model.AdditionalAreaIds.Count > 10)
            modelState.AddModelError(nameof(model.AdditionalAreaIds), "يمكن اختيار 10 مناطق إضافية كحد أقصى.");
        else if (model.AdditionalAreaIds.Count > 0)
        {
            var valid = await db.Areas.CountAsync(a => model.AdditionalAreaIds.Contains(a.Id) && a.IsActive);
            if (valid != model.AdditionalAreaIds.Count)
                modelState.AddModelError(nameof(model.AdditionalAreaIds), "بعض المناطق الإضافية غير صالحة.");
        }
    }

    /// <summary>Validates an optional logo; when <paramref name="required"/> a file must be provided.</summary>
    public static void ValidateLogo(TeamFormBase model, ModelStateDictionary modelState, bool required)
    {
        if (model.Logo == null || model.Logo.Length == 0)
        {
            if (required) modelState.AddModelError(nameof(model.Logo), "شعار الفريق مطلوب.");
            return;
        }
        var error = FileUploadHelper.ValidateImage(model.Logo);
        if (error != null) modelState.AddModelError(nameof(model.Logo), error);
    }
}
