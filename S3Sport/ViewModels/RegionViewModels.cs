using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace S3Sport.ViewModels;

public class RegionSelectViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار المحافظة")]
    [Display(Name = "المحافظة")]
    public int GovernorateId { get; set; }

    [Display(Name = "الولاية")]
    public int? WilayatId { get; set; }

    [Display(Name = "المنطقة")]
    public int? AreaId { get; set; }

    [Display(Name = "القرية")]
    public int? VillageId { get; set; }

    [Display(Name = "اجعلها منطقتي الافتراضية")]
    public bool MakeDefault { get; set; } = true;

    public string? ReturnUrl { get; set; }
    public string? CurrentRegionName { get; set; }
    public IEnumerable<SelectListItem> Governorates { get; set; } = [];
    public IList<SavedRegionItem> SavedRegions { get; set; } = [];
}

public class SavedRegionItem
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>Option returned by the region JSON endpoints.</summary>
public record RegionOption(int Id, string Name);

/// <summary>Result of the region search box: a full path that can be selected directly.</summary>
public record RegionSearchResult(string Text, string Level, int GovernorateId, int? WilayatId, int? AreaId, int? VillageId);

public enum RegionLevel
{
    [Display(Name = "المحافظات")] Governorate = 0,
    [Display(Name = "الولايات")] Wilayat = 1,
    [Display(Name = "المناطق")] Area = 2,
    [Display(Name = "القرى")] Village = 3
}

public class RegionAdminListViewModel
{
    public RegionLevel Level { get; set; }
    public int? ParentId { get; set; }
    public string? Search { get; set; }
    /// <summary>Breadcrumb trail: (level, id, name) from the governorate down to the parent.</summary>
    public IList<(RegionLevel Level, int Id, string Name)> Breadcrumbs { get; set; } = [];
    public IList<RegionAdminRow> Items { get; set; } = [];
    public bool HasChildren => Level != RegionLevel.Village;
}

public class RegionAdminRow
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int ChildCount { get; set; }
}

public class RegionFormViewModel
{
    public RegionLevel Level { get; set; }
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }

    [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
    [StringLength(100, ErrorMessage = "الاسم يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم بالعربية")]
    public string NameAr { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
    [StringLength(100, ErrorMessage = "الاسم يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم بالإنجليزية")]
    public string NameEn { get; set; } = string.Empty;

    [Range(0, 1000, ErrorMessage = "قيمة غير صالحة")]
    [Display(Name = "الترتيب")]
    public int SortOrder { get; set; }

    [Display(Name = "مفعّلة")]
    public bool IsActive { get; set; } = true;
}
