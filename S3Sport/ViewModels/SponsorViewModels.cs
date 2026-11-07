using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using S3Sport.Models;

namespace S3Sport.ViewModels;

public class SponsorFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم الراعي مطلوب"), StringLength(120)][Display(Name = "اسم الراعي")] public string NameAr { get; set; } = string.Empty;
    [StringLength(140)][Display(Name = "سطر قصير (اختياري)")] public string? Tagline { get; set; }
    [Display(Name = "الشعار")] public IFormFile? Logo { get; set; }
    public string? LogoPath { get; set; }
    [StringLength(300)][Display(Name = "رابط الموقع أو الإنستغرام (اختياري)")] public string? WebsiteUrl { get; set; }
    [StringLength(20)][Display(Name = "رقم واتساب (اختياري)")] public string? Phone { get; set; }

    [Display(Name = "مكان الظهور")] public SponsorScope Scope { get; set; }
    [Display(Name = "المحافظة")] public int? GovernorateId { get; set; }
    [Display(Name = "الولاية")] public int? WilayatId { get; set; }
    [Display(Name = "البطولة")] public int? CompetitionId { get; set; }

    [Display(Name = "يبدأ من")] public DateOnly? StartsOn { get; set; }
    [Display(Name = "ينتهي في")] public DateOnly? EndsOn { get; set; }
    [Display(Name = "مفعّل")] public bool IsActive { get; set; } = true;
    [Range(0, 999)][Display(Name = "الترتيب")] public int SortOrder { get; set; }

    public IEnumerable<SelectListItem> Governorates { get; set; } = [];
    public IEnumerable<SelectListItem> Competitions { get; set; } = [];
}

public class SponsorRow
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string Placement { get; set; } = string.Empty;
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool IsActive { get; set; }
    public bool IsRunning { get; set; }
    public int Views { get; set; }
    public int Clicks { get; set; }
}
