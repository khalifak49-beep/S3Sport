using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using S3Sport.Models;

namespace S3Sport.ViewModels;

// ===================== Matches =====================

public class MatchCenterViewModel
{
    public DateOnly Date { get; set; }
    public List<DateOnly> Days { get; set; } = [];
    public bool LiveOnly { get; set; }
    public bool AllRegions { get; set; }
    public bool HasRegion { get; set; }
    public string? RegionName { get; set; }
    public int LiveCount { get; set; }
    public List<CompetitionMatchGroup> Groups { get; set; } = [];
    public HashSet<int> FollowedMatchIds { get; set; } = [];
}

public class CompetitionMatchGroup
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Logo { get; set; }
    public List<MatchRow> Matches { get; set; } = [];
}

public class LineupRow
{
    public int PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? Position { get; set; }
    /// <summary>GK / DF / MF / FW: the row the player stands on in the pitch view.</summary>
    public string? PositionCode { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsStarter { get; set; }
    public bool IsCaptain { get; set; }
}

public class MatchDetailsViewModel
{
    public MatchRow Info { get; set; } = new();
    public MatchLiveDto Live { get; set; } = new();
    public string? StageName { get; set; }
    public List<LineupRow> HomeLineup { get; set; } = [];
    public List<LineupRow> AwayLineup { get; set; } = [];
    public List<(MatchOfficialRole Role, string Name)> Officials { get; set; } = [];
    public string? VenueAddress { get; set; }
    public string? BestPlayerName { get; set; }
    public int? BestPlayerId { get; set; }
    public int? Attendance { get; set; }
    public string? Summary { get; set; }
    public List<MatchRow> HeadToHead { get; set; } = [];
    public (int HomeWins, int Draws, int AwayWins) HeadToHeadSummary { get; set; }
    public FollowButtonViewModel Follow { get; set; } = new();
    public bool CanOperate { get; set; }
    public string? Notes { get; set; }
}

// ===================== Competitions =====================

public class CompetitionDirectoryViewModel
{
    public List<CompetitionListItem> Competitions { get; set; } = [];
    public bool AllRegions { get; set; }
    public bool HasRegion { get; set; }
    public string? RegionName { get; set; }
}

public class PlayerStatRow
{
    public int PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Photo { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int Value { get; set; }
    public int? Secondary { get; set; }
}

public class TeamStatRow
{
    public string TeamName { get; set; } = string.Empty;
    public string TeamSlug { get; set; } = string.Empty;
    public string? TeamLogo { get; set; }
    public int Played { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int CleanSheets { get; set; }
}

public class RoundGroup
{
    public string Title { get; set; } = string.Empty;
    public List<MatchRow> Matches { get; set; } = [];
}

public class CompetitionDetailsViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? Description { get; set; }
    public string? OrganizerName { get; set; }
    public string RegionText { get; set; } = string.Empty;
    public string SportName { get; set; } = string.Empty;
    public List<SelectListItem> Editions { get; set; } = [];
    public int? EditionId { get; set; }
    public string? SeasonName { get; set; }
    public CompetitionFormat? Format { get; set; }
    public CompetitionSeasonStatus? Status { get; set; }
    public string? Regulations { get; set; }
    public string? ChampionName { get; set; }
    public string? ChampionSlug { get; set; }
    public string? ChampionLogo { get; set; }
    public List<StandingsTableView> Tables { get; set; } = [];
    public List<RoundGroup> Fixtures { get; set; } = [];
    public List<RoundGroup> Results { get; set; } = [];
    public List<PlayerStatRow> TopScorers { get; set; } = [];
    public List<PlayerStatRow> TopAssists { get; set; } = [];
    public List<PlayerStatRow> BestPlayers { get; set; } = [];
    public List<PlayerStatRow> MostAppearances { get; set; } = [];
    public List<PlayerStatRow> MostCards { get; set; } = [];
    public List<TeamStatRow> TeamStats { get; set; } = [];
    public int TeamsCount { get; set; }
    public int MatchesPlayed { get; set; }
    public int MatchesTotal { get; set; }
    public int TotalGoals { get; set; }
    public FollowButtonViewModel Follow { get; set; } = new();
    public bool CanManage { get; set; }
}

public class StandingsPageViewModel
{
    public bool AllRegions { get; set; }
    public bool HasRegion { get; set; }
    public string? RegionName { get; set; }
    public List<StandingsBlock> Blocks { get; set; } = [];
}

public class StandingsBlock
{
    public string CompetitionName { get; set; } = string.Empty;
    public string CompetitionSlug { get; set; } = string.Empty;
    public string? CompetitionLogo { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public List<StandingsTableView> Tables { get; set; } = [];
}

// ===================== News =====================

public class NewsListItem
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? CoverImagePath { get; set; }
    public NewsCategory Category { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; }
    public bool HasVideo { get; set; }
    public string? TeamName { get; set; }
    public string? CompetitionName { get; set; }
    public int ViewCount { get; set; }
}

public class NewsListViewModel
{
    public PagedResult<NewsListItem> Results { get; set; } = new();
    public NewsCategory? Category { get; set; }
    public string? Search { get; set; }
    public bool? Published { get; set; }
}

public class NewsDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? CoverImagePath { get; set; }
    public NewsCategory Category { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoEmbedUrl { get; set; }
    public int ViewCount { get; set; }
    public string? AuthorName { get; set; }
    public string? TeamName { get; set; }
    public string? TeamSlug { get; set; }
    public string? PlayerName { get; set; }
    public int? PlayerId { get; set; }
    public string? CompetitionName { get; set; }
    public string? CompetitionSlug { get; set; }
    public string? MatchTitle { get; set; }
    public int? MatchId { get; set; }
    public List<MediaItem> Media { get; set; } = [];
    public List<NewsListItem> Related { get; set; } = [];
}

public class MediaItem
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public MediaType Type { get; set; }
}

public class NewsFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "العنوان مطلوب"), StringLength(250, MinimumLength = 5, ErrorMessage = "العنوان بين 5 و250 حرفاً")]
    [Display(Name = "العنوان")] public string TitleAr { get; set; } = string.Empty;
    [StringLength(500)][Display(Name = "الملخص")] public string? Summary { get; set; }
    [Required(ErrorMessage = "نص الخبر مطلوب")][Display(Name = "نص الخبر")] public string Body { get; set; } = string.Empty;
    [Display(Name = "التصنيف")] public NewsCategory Category { get; set; }
    [StringLength(300), Url(ErrorMessage = "الرابط غير صالح")][Display(Name = "رابط فيديو (يوتيوب)")] public string? VideoUrl { get; set; }
    [Display(Name = "الفريق")] public int? TeamId { get; set; }
    [Display(Name = "اللاعب")] public int? PlayerId { get; set; }
    [Display(Name = "البطولة")] public int? CompetitionId { get; set; }
    [Display(Name = "المباراة")] public int? MatchId { get; set; }
    [Display(Name = "نشر الآن")] public bool IsPublished { get; set; }
    [Display(Name = "صورة الغلاف")] public IFormFile? Cover { get; set; }
    public string? CoverImagePath { get; set; }
    [Display(Name = "صور إضافية للمعرض")] public List<IFormFile> Gallery { get; set; } = [];
    public List<MediaItem> ExistingMedia { get; set; } = [];
    public string? RowVersion { get; set; }
    public IEnumerable<SelectListItem> Teams { get; set; } = [];
    public IEnumerable<SelectListItem> Players { get; set; } = [];
    public IEnumerable<SelectListItem> Competitions { get; set; } = [];
    public IEnumerable<SelectListItem> Matches { get; set; } = [];
}

// ===================== Favorites (extended) =====================

public class FavoriteItem
{
    public FollowTargetType Type { get; set; }
    public int TargetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string? Subtitle { get; set; }
    public string Url { get; set; } = string.Empty;
    public FollowButtonViewModel Follow { get; set; } = new();
}
