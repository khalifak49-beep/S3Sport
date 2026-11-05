using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using S3Sport.Models;

namespace S3Sport.ViewModels;

// ===================== Sports / seasons / venues =====================

public class SportFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "الاسم بالعربية مطلوب"), StringLength(80)][Display(Name = "الاسم بالعربية")] public string NameAr { get; set; } = string.Empty;
    [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب"), StringLength(80)][Display(Name = "الاسم بالإنجليزية")] public string NameEn { get; set; } = string.Empty;
    [Required(ErrorMessage = "الرمز مطلوب"), StringLength(30), RegularExpression("^[a-z0-9-]+$", ErrorMessage = "حروف إنجليزية صغيرة وأرقام فقط")]
    [Display(Name = "الرمز")] public string Code { get; set; } = string.Empty;
    [StringLength(60)][Display(Name = "أيقونة (Font Awesome)")] public string? IconClass { get; set; }
    [Range(1, 30, ErrorMessage = "قيمة غير صالحة")][Display(Name = "عدد اللاعبين في الملعب")] public int PlayersOnField { get; set; } = 11;
    [Range(1, 10, ErrorMessage = "قيمة غير صالحة")][Display(Name = "عدد الأشواط")] public int PeriodsCount { get; set; } = 2;
    [Range(1, 120, ErrorMessage = "قيمة غير صالحة")][Display(Name = "مدة الشوط (دقيقة)")] public int PeriodDurationMinutes { get; set; } = 45;
    [Range(0, 10)][Display(Name = "نقاط الفوز")] public int PointsForWin { get; set; } = 3;
    [Range(0, 10)][Display(Name = "نقاط التعادل")] public int PointsForDraw { get; set; } = 1;
    [Range(0, 10)][Display(Name = "نقاط الخسارة")] public int PointsForLoss { get; set; }
    [Display(Name = "مفعّلة")] public bool IsActive { get; set; } = true;
    public IList<SportPosition> Positions { get; set; } = [];
}

public class SeasonFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم الموسم مطلوب"), StringLength(30)][Display(Name = "اسم الموسم")] public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage = "تاريخ البداية مطلوب")][Display(Name = "تاريخ البداية")] public DateOnly? StartDate { get; set; }
    [Required(ErrorMessage = "تاريخ النهاية مطلوب")][Display(Name = "تاريخ النهاية")] public DateOnly? EndDate { get; set; }
    [Display(Name = "الموسم الحالي")] public bool IsCurrent { get; set; }
}

public class SeasonRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public int EditionsCount { get; set; }
}

public class VenueFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم الملعب مطلوب"), StringLength(150)][Display(Name = "اسم الملعب")] public string NameAr { get; set; } = string.Empty;
    [StringLength(150)][Display(Name = "الاسم بالإنجليزية")] public string? NameEn { get; set; }
    [Display(Name = "المحافظة")] public int? GovernorateId { get; set; }
    [Display(Name = "الولاية")] public int? WilayatId { get; set; }
    [Display(Name = "المنطقة")] public int? AreaId { get; set; }
    [StringLength(300)][Display(Name = "العنوان")] public string? Address { get; set; }
    [Range(0, 200000, ErrorMessage = "قيمة غير صالحة")][Display(Name = "السعة")] public int? Capacity { get; set; }
    public IEnumerable<SelectListItem> Governorates { get; set; } = [];
}

public class VenueRow
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? AreaText { get; set; }
    public int? Capacity { get; set; }
    public int MatchesCount { get; set; }
}

// ===================== Players =====================

public class PlayerFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم اللاعب مطلوب"), StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم بين 3 و150 حرفاً")]
    [Display(Name = "الاسم الكامل")] public string FullNameAr { get; set; } = string.Empty;
    [StringLength(150)][Display(Name = "الاسم بالإنجليزية")] public string? FullNameEn { get; set; }
    [Display(Name = "تاريخ الميلاد")] public DateOnly? DateOfBirth { get; set; }
    [StringLength(60)][Display(Name = "الجنسية")] public string? Nationality { get; set; } = "عُمان";
    [Display(Name = "مركز اللعب")] public int? PositionId { get; set; }
    [StringLength(1000)][Display(Name = "نبذة")] public string? Bio { get; set; }
    [Display(Name = "الصورة الشخصية")] public IFormFile? Photo { get; set; }
    public string? ExistingPhotoPath { get; set; }
    [Display(Name = "حذف الصورة الحالية")] public bool RemovePhoto { get; set; }

    /// <summary>Team assignment on creation (admin) or the managed team (team manager).</summary>
    [Display(Name = "الفريق")] public int? TeamId { get; set; }
    [Range(1, 99, ErrorMessage = "رقم القميص بين 1 و99")][Display(Name = "رقم القميص")] public int? ShirtNumber { get; set; }
    [Display(Name = "قائد الفريق")] public bool IsCaptain { get; set; }
    [Display(Name = "تاريخ الانضمام")] public DateOnly? JoinedOn { get; set; }

    public string? RowVersion { get; set; }
    public string? TeamName { get; set; }
    public IEnumerable<SelectListItem> Positions { get; set; } = [];
    public IEnumerable<SelectListItem> Teams { get; set; } = [];
}

public class PlayerListItem
{
    public int Id { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public string? Position { get; set; }
    public string? TeamName { get; set; }
    public int? TeamId { get; set; }
    public int? ShirtNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int Goals { get; set; }
    public int Appearances { get; set; }
}

public class PlayerListViewModel
{
    public PagedResult<PlayerListItem> Results { get; set; } = new();
    public string? Search { get; set; }
    public int? TeamId { get; set; }
    public bool FreeAgentsOnly { get; set; }
    public IEnumerable<SelectListItem> Teams { get; set; } = [];
}

public class PlayerHistoryRow
{
    public string TeamName { get; set; } = string.Empty;
    public string? TeamSlug { get; set; }
    public string? TeamLogo { get; set; }
    public int? ShirtNumber { get; set; }
    public DateOnly JoinedOn { get; set; }
    public DateOnly? LeftOn { get; set; }
    public bool IsActive { get; set; }
}

public class PlayerTransferRow
{
    public DateOnly TransferDate { get; set; }
    public string? FromTeam { get; set; }
    public string? ToTeam { get; set; }
    public string? Notes { get; set; }
}

public class PlayerSeasonStatRow
{
    public string CompetitionName { get; set; } = string.Empty;
    public string? CompetitionSlug { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int Appearances { get; set; }
    public int Starts { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int BestPlayerAwards { get; set; }
}

/// <summary>Player profile — used by the admin details page and the public player page.</summary>
public class PlayerDetailsViewModel
{
    public int Id { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string? FullNameEn { get; set; }
    public string? PhotoPath { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Age => DateOfBirth is { } d ? (int)((DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - d.DayNumber) / 365.25) : null;
    public string? Nationality { get; set; }
    public string? Position { get; set; }
    public string? Bio { get; set; }
    public int? CurrentTeamId { get; set; }
    public string? CurrentTeamName { get; set; }
    public string? CurrentTeamSlug { get; set; }
    public string? CurrentTeamLogo { get; set; }
    public int? ShirtNumber { get; set; }
    public IList<PlayerHistoryRow> History { get; set; } = [];
    public IList<PlayerTransferRow> Transfers { get; set; } = [];
    public IList<PlayerSeasonStatRow> Stats { get; set; } = [];
    public FollowButtonViewModel Follow { get; set; } = new();
    public IEnumerable<SelectListItem> TransferTeams { get; set; } = [];
    public bool CanAdminEdit { get; set; }
    public int TotalGoals => Stats.Sum(s => s.Goals);
    public int TotalAppearances => Stats.Sum(s => s.Appearances);
    public int TotalAssists => Stats.Sum(s => s.Assists);
}

/// <summary>Team-manager self-service: their team and its squad.</summary>
public class MyTeamViewModel
{
    public int TeamId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public TeamStatus Status { get; set; }
    public IList<PlayerListItem> Squad { get; set; } = [];
    public IList<PlayerListItem> FreeAgents { get; set; } = [];
    public string? FreeAgentSearch { get; set; }
}

public class MyTeamInfoViewModel
{
    public int TeamId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    [StringLength(30)][Display(Name = "الاسم المختصر")] public string? ShortName { get; set; }
    [StringLength(2000)][Display(Name = "نبذة عن الفريق")] public string? Description { get; set; }
    [StringLength(150), EmailAddress(ErrorMessage = "البريد غير صالح")][Display(Name = "البريد الإلكتروني للتواصل")] public string? ContactEmail { get; set; }
    [StringLength(300), Url(ErrorMessage = "الرابط غير صالح")][Display(Name = "رابط التواصل الاجتماعي")] public string? SocialUrl { get; set; }
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "اللون غير صالح")][Display(Name = "اللون الأساسي")] public string? PrimaryColor { get; set; }
    [Display(Name = "شعار جديد")] public IFormFile? Logo { get; set; }
    public string? LogoPath { get; set; }
    public string? RowVersion { get; set; }
}

/// <summary>All matches of a managed team; friendlies it organised can be edited here and run live.</summary>
public class MyTeamMatchesViewModel
{
    public int TeamId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public IList<MatchRow> Upcoming { get; set; } = [];
    public IList<MatchRow> Played { get; set; } = [];
    /// <summary>Matches this user may record live (organised friendlies, or competition matches assigned to them).</summary>
    public HashSet<int> Operable { get; set; } = [];
    /// <summary>Friendlies organised by this team (editable while not started).</summary>
    public HashSet<int> Organized { get; set; } = [];
}

/// <summary>A friendly scheduled by a team manager. The opponent is a platform team or a guest team typed by name.</summary>
public class TeamMatchFormViewModel
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string? TeamName { get; set; }
    [Display(Name = "الخصم")] public int? OpponentTeamId { get; set; }
    [StringLength(150)][Display(Name = "أو اكتب اسم فريق غير مسجل في المنصة")] public string? OpponentGuestName { get; set; }
    [Display(Name = "فريقي يلعب على أرضه (المستضيف)")] public bool IsHome { get; set; } = true;
    [Required(ErrorMessage = "موعد المباراة مطلوب")][Display(Name = "الموعد (بتوقيت مسقط)")] public DateTime? KickoffLocal { get; set; }
    [Display(Name = "الملعب")] public int? VenueId { get; set; }
    [Display(Name = "الحالة")] public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    [StringLength(150)][Display(Name = "حكم الساحة")] public string? RefereeName { get; set; }
    [StringLength(500)][Display(Name = "ملاحظات")] public string? Notes { get; set; }
    public string? RowVersion { get; set; }
    public bool HasStarted { get; set; }
    public IEnumerable<SelectListItem> Opponents { get; set; } = [];
    public IEnumerable<SelectListItem> Venues { get; set; } = [];
}

// ===================== Competitions =====================

public class CompetitionFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم البطولة مطلوب"), StringLength(150)][Display(Name = "اسم البطولة")] public string NameAr { get; set; } = string.Empty;
    [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب"), StringLength(150), RegularExpression(@"^[A-Za-z0-9 .'\-&]+$", ErrorMessage = "حروف إنجليزية وأرقام فقط")]
    [Display(Name = "الاسم بالإنجليزية")] public string NameEn { get; set; } = string.Empty;
    [Display(Name = "الرياضة")] public int SportId { get; set; } = 1;
    [StringLength(2000)][Display(Name = "الوصف")] public string? Description { get; set; }
    [StringLength(150)][Display(Name = "الجهة المنظمة")] public string? OrganizerName { get; set; }
    [Display(Name = "المحافظة (اتركها فارغة لبطولة على مستوى السلطنة)")] public int? GovernorateId { get; set; }
    [Display(Name = "الولاية")] public int? WilayatId { get; set; }
    [Display(Name = "المنطقة")] public int? AreaId { get; set; }
    [Display(Name = "مفعّلة")] public bool IsActive { get; set; } = true;
    [Display(Name = "الشعار")] public IFormFile? Logo { get; set; }
    public string? LogoPath { get; set; }
    public string? RowVersion { get; set; }
    public IEnumerable<SelectListItem> Sports { get; set; } = [];
    public IEnumerable<SelectListItem> Governorates { get; set; } = [];
}

public class CompetitionListItem
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string? RegionText { get; set; }
    public bool IsActive { get; set; }
    public int EditionsCount { get; set; }
    public string? CurrentEditionName { get; set; }
    public CompetitionSeasonStatus? CurrentEditionStatus { get; set; }
}

public class CompetitionAdminDetailsViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? RegionText { get; set; }
    public IList<EditionRow> Editions { get; set; } = [];
    public IList<(string UserId, string FullName, string? Email)> Managers { get; set; } = [];
}

public class EditionRow
{
    public int Id { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public CompetitionFormat Format { get; set; }
    public CompetitionSeasonStatus Status { get; set; }
    public int TeamsCount { get; set; }
    public int MatchesCount { get; set; }
    public int FinishedCount { get; set; }
}

public class EditionFormViewModel
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public string? CompetitionName { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "اختر الموسم")][Display(Name = "الموسم")] public int SeasonId { get; set; }
    [Display(Name = "نظام البطولة")] public CompetitionFormat Format { get; set; }
    [Display(Name = "الحالة")] public CompetitionSeasonStatus Status { get; set; } = CompetitionSeasonStatus.Registration;
    [Display(Name = "تاريخ البداية")] public DateOnly? StartDate { get; set; }
    [Display(Name = "تاريخ النهاية")] public DateOnly? EndDate { get; set; }
    [Range(0, 10)][Display(Name = "نقاط الفوز")] public int PointsForWin { get; set; } = 3;
    [Range(0, 10)][Display(Name = "نقاط التعادل")] public int PointsForDraw { get; set; } = 1;
    [Range(0, 10)][Display(Name = "نقاط الخسارة")] public int PointsForLoss { get; set; }
    /// <summary>Ordered tie-breakers chosen in up to five dropdowns.</summary>
    [Display(Name = "معايير كسر التعادل بالترتيب")] public List<TieBreakerCriterion?> TieBreakers { get; set; } = [];
    [StringLength(4000)][Display(Name = "لائحة البطولة")] public string? Regulations { get; set; }
    public string? RowVersion { get; set; }
    public IEnumerable<SelectListItem> Seasons { get; set; } = [];
}

/// <summary>Edition management hub: teams, groups, fixtures and standings.</summary>
public class EditionManageViewModel
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public string CompetitionName { get; set; } = string.Empty;
    public string CompetitionSlug { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public CompetitionFormat Format { get; set; }
    public CompetitionSeasonStatus Status { get; set; }
    public string TieBreakersText { get; set; } = string.Empty;
    public IList<EditionTeamRow> Teams { get; set; } = [];
    public IList<StageView> Stages { get; set; } = [];
    public IList<MatchRow> Matches { get; set; } = [];
    public IList<StandingsTableView> Tables { get; set; } = [];
    public IEnumerable<SelectListItem> AvailableTeams { get; set; } = [];
    public IEnumerable<SelectListItem> Venues { get; set; } = [];
    public bool HasGroupStage => Stages.Any(s => s.StageType == StageType.Group);
    public bool HasKnockoutStage => Stages.Any(s => s.StageType == StageType.Knockout);
}

public class EditionTeamRow
{
    public int CompetitionTeamId { get; set; }
    public int TeamId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? GroupName { get; set; }
    public int? Seed { get; set; }
    public bool HasMatches { get; set; }
}

public record GroupOption(int Id, string Name);

public class StageView
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public StageType StageType { get; set; }
    public int Legs { get; set; }
    public IList<GroupOption> Groups { get; set; } = [];
}

// ===================== Matches =====================

public class MatchRow
{
    public int Id { get; set; }
    public DateTime KickoffAt { get; set; }
    public MatchStatus Status { get; set; }
    public string HomeName { get; set; } = string.Empty;
    public string? HomeLogo { get; set; }
    public string AwayName { get; set; } = string.Empty;
    public string? AwayLogo { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int? HomePenaltyScore { get; set; }
    public int? AwayPenaltyScore { get; set; }
    public string? RoundName { get; set; }
    public int? RoundNumber { get; set; }
    public string? StageName { get; set; }
    public string? GroupName { get; set; }
    public string? CompetitionName { get; set; }
    public string? VenueName { get; set; }
    public string? OperatorName { get; set; }
    public int CurrentPeriod { get; set; }
    public int PeriodDurationMinutes { get; set; } = 45;
    public string? HomeSlug { get; set; }
    public string? AwaySlug { get; set; }
    public string? CompetitionSlug { get; set; }
    public string? CompetitionLogo { get; set; }
    public int? CompetitionId { get; set; }
    public bool IsLive => Status is MatchStatus.Live or MatchStatus.HalfTime;
    public bool HasScore => Status is MatchStatus.Live or MatchStatus.HalfTime or MatchStatus.Finished;
    public string? PeriodStartIso => CurrentPeriodStartedAt is { } p ? DateTime.SpecifyKind(p, DateTimeKind.Utc).ToString("O") : null;
    public DateTime? CurrentPeriodStartedAt { get; set; }
}

public class MatchListViewModel
{
    public PagedResult<MatchRow> Results { get; set; } = new();
    public int? EditionId { get; set; }
    public MatchStatus? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public IEnumerable<SelectListItem> Editions { get; set; } = [];
}

public class MatchFormViewModel
{
    public int Id { get; set; }
    [Display(Name = "البطولة (اتركها فارغة للمباراة الودية)")] public int? CompetitionSeasonId { get; set; }
    [Display(Name = "المرحلة")] public int? StageId { get; set; }
    [Display(Name = "المجموعة")] public int? GroupId { get; set; }
    [Range(1, 200, ErrorMessage = "قيمة غير صالحة")][Display(Name = "رقم الجولة")] public int? RoundNumber { get; set; }
    [StringLength(100)][Display(Name = "اسم الجولة")] public string? RoundName { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "اختر الفريق المستضيف")][Display(Name = "الفريق المستضيف")] public int HomeTeamId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "اختر الفريق الضيف")][Display(Name = "الفريق الضيف")] public int AwayTeamId { get; set; }
    [Required(ErrorMessage = "موعد المباراة مطلوب")][Display(Name = "الموعد (بتوقيت مسقط)")] public DateTime? KickoffLocal { get; set; }
    [Display(Name = "الملعب")] public int? VenueId { get; set; }
    [Display(Name = "الحالة")] public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    [Display(Name = "مسؤول تسجيل الأحداث")] public string? OperatorUserId { get; set; }
    [StringLength(150)][Display(Name = "حكم الساحة")] public string? RefereeName { get; set; }
    [StringLength(150)][Display(Name = "المساعد الأول")] public string? Assistant1Name { get; set; }
    [StringLength(150)][Display(Name = "المساعد الثاني")] public string? Assistant2Name { get; set; }
    [StringLength(500)][Display(Name = "ملاحظات")] public string? Notes { get; set; }

    // Manual result (status = Finished, match not covered live).
    [Range(0, 99, ErrorMessage = "قيمة غير صالحة")][Display(Name = "أهداف المستضيف")] public int? ResultHome { get; set; }
    [Range(0, 99, ErrorMessage = "قيمة غير صالحة")][Display(Name = "أهداف الضيف")] public int? ResultAway { get; set; }
    [Range(0, 99, ErrorMessage = "قيمة غير صالحة")][Display(Name = "ترجيح المستضيف")] public int? PenaltyHome { get; set; }
    [Range(0, 99, ErrorMessage = "قيمة غير صالحة")][Display(Name = "ترجيح الضيف")] public int? PenaltyAway { get; set; }

    public string? RowVersion { get; set; }
    public bool HasStarted { get; set; }
    public IEnumerable<SelectListItem> Editions { get; set; } = [];
    public IEnumerable<SelectListItem> Stages { get; set; } = [];
    public IEnumerable<SelectListItem> Groups { get; set; } = [];
    public IEnumerable<SelectListItem> Teams { get; set; } = [];
    public IEnumerable<SelectListItem> Venues { get; set; } = [];
    public IEnumerable<SelectListItem> Operators { get; set; } = [];
}

// ===================== Standings (shared admin + public) =====================

public class StandingsTableView
{
    public string? Title { get; set; }
    public IList<StandingRow> Rows { get; set; } = [];
}

public class StandingRow
{
    public int Position { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamSlug { get; set; } = string.Empty;
    public string? TeamLogo { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
    public int PointsAdjustment { get; set; }
    public string? Form { get; set; }
    public bool Qualifies { get; set; }
}
