using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using S3Sport.Models;

namespace S3Sport.ViewModels;

/// <summary>Fields shared by the team application form (users) and the team edit form (admins).</summary>
public abstract class TeamFormBase
{
    [Required(ErrorMessage = "اسم الفريق بالعربية مطلوب")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين 2 و150 حرفاً")]
    [Display(Name = "اسم الفريق بالعربية")]
    public string NameAr { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الفريق بالإنجليزية مطلوب")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين 2 و150 حرفاً")]
    [RegularExpression(@"^[A-Za-z0-9 .'\-&]+$", ErrorMessage = "استخدم حروفاً إنجليزية وأرقاماً فقط")]
    [Display(Name = "اسم الفريق بالإنجليزية")]
    public string NameEn { get; set; } = string.Empty;

    [Range(1900, 2100, ErrorMessage = "سنة التأسيس غير صالحة")]
    [Display(Name = "سنة التأسيس")]
    public int? FoundedYear { get; set; }

    [StringLength(2000, ErrorMessage = "الوصف يجب ألا يتجاوز 2000 حرف")]
    [Display(Name = "نبذة عن الفريق")]
    public string? Description { get; set; }

    [StringLength(150)]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [Display(Name = "البريد الإلكتروني للتواصل")]
    public string? ContactEmail { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار المحافظة")]
    [Display(Name = "المحافظة")]
    public int GovernorateId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار الولاية")]
    [Display(Name = "الولاية")]
    public int WilayatId { get; set; }

    /// <summary>Required, except in the application form where a new area name may be proposed instead.</summary>
    [Display(Name = "المنطقة")]
    public int? AreaId { get; set; }

    /// <summary>Only the user application form allows typing an area that is not in the list yet.</summary>
    public virtual bool AllowsProposedArea => false;
    public virtual string? ProposedArea => null;

    [Display(Name = "القرية (اختياري)")]
    public int? VillageId { get; set; }

    [Display(Name = "مناطق إضافية يظهر فيها الفريق")]
    public List<int> AdditionalAreaIds { get; set; } = [];

    [Display(Name = "شعار الفريق")]
    public IFormFile? Logo { get; set; }
    public string? ExistingLogoPath { get; set; }

    public IEnumerable<SelectListItem> Governorates { get; set; } = [];
}

public class TeamApplicationFormViewModel : TeamFormBase
{
    public int Id { get; set; }

    [Required(ErrorMessage = "رقم التواصل مطلوب")]
    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    [StringLength(30)]
    [Display(Name = "رقم التواصل")]
    public string ContactPhone { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 2, ErrorMessage = "اسم المنطقة يجب أن يكون بين 2 و100 حرف")]
    [Display(Name = "اسم المنطقة (إذا لم تجدها في القائمة)")]
    public string? ProposedAreaName { get; set; }

    public override bool AllowsProposedArea => true;
    public override string? ProposedArea => ProposedAreaName;

    public string? RowVersion { get; set; }
    public string? ReviewNotes { get; set; }
}

public class TeamEditViewModel : TeamFormBase
{
    public int Id { get; set; }

    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    [StringLength(30)]
    [Display(Name = "رقم التواصل")]
    public string? ContactPhone { get; set; }

    [StringLength(30)]
    [Display(Name = "الاسم المختصر")]
    public string? ShortName { get; set; }

    [StringLength(300)]
    [Url(ErrorMessage = "الرابط غير صالح")]
    [Display(Name = "رابط التواصل الاجتماعي")]
    public string? SocialUrl { get; set; }

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "اللون غير صالح")]
    [Display(Name = "اللون الأساسي")]
    public string? PrimaryColor { get; set; }

    [Display(Name = "الحالة")]
    public TeamStatus Status { get; set; } = TeamStatus.Approved;

    [Display(Name = "حذف الشعار الحالي")]
    public bool RemoveLogo { get; set; }

    public string? RowVersion { get; set; }
    public string? Slug { get; set; }
    public IList<TeamMemberRow> Members { get; set; } = [];
}

public class TeamMemberRow
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public TeamMemberRole Role { get; set; }
    public bool IsActive { get; set; }
}

public class TeamApplicationListItem
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string? ApplicantEmail { get; set; }
    public string RegionText { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public TeamStatus Status { get; set; }
    public int? CreatedTeamId { get; set; }
}

public class TeamApplicationListViewModel
{
    public PagedResult<TeamApplicationListItem> Results { get; set; } = new();
    public TeamStatus? Status { get; set; }
    public string? Search { get; set; }
    public Dictionary<TeamStatus, int> StatusCounts { get; set; } = [];
}

public class TeamApplicationDetailsViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string GovernorateName { get; set; } = string.Empty;
    public string WilayatName { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    /// <summary>True when the area was typed by the applicant and does not exist yet.</summary>
    public bool IsProposedArea { get; set; }
    public string? VillageName { get; set; }
    public IList<string> AdditionalAreas { get; set; } = [];
    public int? FoundedYear { get; set; }
    public string? Description { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public TeamStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string? ApplicantEmail { get; set; }
    public string? ApplicantPhone { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public int? CreatedTeamId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    /// <summary>Approved teams already registered with the same Arabic name (helps spot duplicates).</summary>
    public IList<string> SimilarTeams { get; set; } = [];
}

public class TeamListItem
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public TeamStatus Status { get; set; }
    public string? PrimaryAreaText { get; set; }
    public int PlayersCount { get; set; }
    public string? ManagerName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TeamListViewModel
{
    public PagedResult<TeamListItem> Results { get; set; } = new();
    public string? Search { get; set; }
    public TeamStatus? Status { get; set; }
    public int? GovernorateId { get; set; }
    public string Sort { get; set; } = "name";
    public IEnumerable<SelectListItem> Governorates { get; set; } = [];
}

public class TeamCardViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? AreaText { get; set; }
    public int? FoundedYear { get; set; }
    public int FollowersCount { get; set; }
    public bool IsFollowed { get; set; }
}

/// <summary>Public teams directory.</summary>
public class TeamDirectoryViewModel
{
    public PagedResult<TeamCardViewModel> Results { get; set; } = new();
    public string? Search { get; set; }
    /// <summary>True = all of Oman; false = only the visitor's current region.</summary>
    public bool AllRegions { get; set; }
    public bool HasRegion { get; set; }
    public string? RegionName { get; set; }
}

/// <summary>Public team page.</summary>
public class TeamDetailsViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? LogoPath { get; set; }
    public int? FoundedYear { get; set; }
    public string? Description { get; set; }
    public string? ContactEmail { get; set; }
    public string? SocialUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string? PrimaryAreaText { get; set; }
    public IList<string> OtherAreas { get; set; } = [];
    public IList<TeamPlayerRow> Players { get; set; } = [];
    public IList<AchievementRow> Achievements { get; set; } = [];
    public FollowButtonViewModel Follow { get; set; } = new();
    public bool CanAdminEdit { get; set; }
    public bool CanManage { get; set; }
    public List<MatchRow> UpcomingMatches { get; set; } = [];
    public List<MatchRow> RecentMatches { get; set; } = [];
    public List<CompetitionListItem> Competitions { get; set; } = [];
    public List<NewsListItem> News { get; set; } = [];
    /// <summary>All-time totals across every competition season.</summary>
    public TeamStatRow Totals { get; set; } = new();
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
}

public class TeamPlayerRow
{
    public int PlayerId { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public int? ShirtNumber { get; set; }
    public string? Position { get; set; }
    public bool IsCaptain { get; set; }
}

public class AchievementRow
{
    public string TitleAr { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Description { get; set; }
}
