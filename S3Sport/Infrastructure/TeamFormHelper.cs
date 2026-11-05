using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
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

    /// <summary>
    /// Full validation of a team application (region, logo, duplicate names, pending-request limit).
    /// Shared by the "register a team" sign-up and the signed-in application form. <paramref name="applicantUserId"/>
    /// is null for a brand-new account (it has no earlier requests).
    /// </summary>
    public static async Task ValidateApplicationAsync(ApplicationDbContext db, TeamApplicationFormViewModel model, ModelStateDictionary modelState,
        string? applicantUserId, bool logoRequired, int? ignoreApplicationId = null)
    {
        // Returns the cleaned typed area name, or null when an existing area is used (it may also pick
        // the existing area automatically if the typed name matches one).
        model.ProposedAreaName = await ValidateRegionAsync(db, model, modelState);
        await ValidateAdditionalAreasAsync(db, model, modelState);
        ValidateLogo(model, modelState, logoRequired);

        var name = model.NameAr.Trim();
        if (model.AreaId != null && await db.Teams.AnyAsync(t => t.NameAr == name && t.TeamAreas.Any(ta => ta.AreaId == model.AreaId)))
            modelState.AddModelError(nameof(model.NameAr), "يوجد فريق مسجل بنفس الاسم في هذه المنطقة.");
        if (await db.TeamApplications.AnyAsync(a => a.Id != ignoreApplicationId && a.Status == TeamStatus.Pending
                && a.NameAr == name && a.WilayatId == model.WilayatId))
            modelState.AddModelError(nameof(model.NameAr), "يوجد طلب قيد المراجعة لفريق بنفس الاسم في هذه الولاية.");
        if (applicantUserId != null && ignoreApplicationId == null
            && await db.TeamApplications.CountAsync(a => a.ApplicantUserId == applicantUserId && a.Status == TeamStatus.Pending) >= 3)
            modelState.AddModelError(string.Empty, "لديك 3 طلبات قيد المراجعة. انتظر مراجعتها قبل تقديم طلب جديد.");
    }

    /// <summary>Copies the form into the application entity (additional areas are diffed, not replaced).</summary>
    public static void MapApplication(TeamApplicationFormViewModel model, TeamApplication application)
    {
        application.NameAr = model.NameAr.Trim();
        application.NameEn = model.NameEn.Trim();
        application.FoundedYear = model.FoundedYear;
        application.Description = model.Description?.Trim();
        application.ContactPhone = model.ContactPhone.Trim();
        application.ContactEmail = model.ContactEmail?.Trim();
        application.GovernorateId = model.GovernorateId;
        application.WilayatId = model.WilayatId;
        application.AreaId = model.AreaId;
        application.ProposedAreaName = model.AreaId == null ? model.ProposedAreaName : null;
        application.VillageId = model.VillageId;

        // Diff instead of clear+add: re-adding the same composite key in one context would conflict.
        foreach (var removed in application.AdditionalAreas.Where(x => !model.AdditionalAreaIds.Contains(x.AreaId)).ToList())
            application.AdditionalAreas.Remove(removed);
        foreach (var areaId in model.AdditionalAreaIds.Where(id => application.AdditionalAreas.All(x => x.AreaId != id)))
            application.AdditionalAreas.Add(new TeamApplicationArea { AreaId = areaId });
    }
}
