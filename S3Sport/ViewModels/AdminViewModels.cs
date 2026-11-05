using Microsoft.AspNetCore.Mvc.Rendering;
using S3Sport.Models;

namespace S3Sport.ViewModels;

public class DashboardViewModel
{
    public int TotalUsers { get; set; }
    public int ApprovedTeams { get; set; }
    public int PendingApplications { get; set; }
    public int ActiveCompetitions { get; set; }
    public int UpcomingMatches { get; set; }
    public int LiveMatches { get; set; }
    public int TotalPlayers { get; set; }
    public int NewUsersThisWeek { get; set; }
    public IList<TeamApplicationListItem> LatestApplications { get; set; } = [];
    public IList<AuditLogRow> RecentActivity { get; set; } = [];
    public IList<ChartPoint> TeamsPerGovernorate { get; set; } = [];
    public IList<ChartPoint> UsersPerRole { get; set; } = [];
}

public record ChartPoint(string Label, int Value);

public class AuditLogRow
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

public class AuditLogListViewModel
{
    public PagedResult<AuditLogRow> Results { get; set; } = new();
    public string? Search { get; set; }
    public string? EntityName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public IEnumerable<SelectListItem> EntityNames { get; set; } = [];
}

public class UserListItem
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public IList<string> Roles { get; set; } = [];
}

public class UserListViewModel
{
    public PagedResult<UserListItem> Results { get; set; } = new();
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? Active { get; set; }
}

public class UserRolesViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public IList<RoleCheckbox> Roles { get; set; } = [];
    public IList<string> ManagedTeams { get; set; } = [];
}

public class RoleCheckbox
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Selected { get; set; }
    /// <summary>Only a SuperAdmin may grant or revoke SuperAdmin/Admin.</summary>
    public bool Disabled { get; set; }
}

public class RoleSummary
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public IList<string> Permissions { get; set; } = [];
}

public class NotificationItem
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HomeIndexViewModel
{
    public string? RegionName { get; set; }
    public bool HasRegion { get; set; }
    public IList<TeamCardViewModel> Teams { get; set; } = [];
    public int TotalTeamsInRegion { get; set; }
    /// <summary>Most-followed teams in the region; falls back to all of Oman when the region has none.</summary>
    public IList<TeamCardViewModel> MostFollowed { get; set; } = [];
    public bool MostFollowedIsNational { get; set; }
    /// <summary>Teams the signed-in user follows, from any region.</summary>
    public IList<TeamCardViewModel> FollowedTeams { get; set; } = [];
    public IList<MatchRow> LiveMatches { get; set; } = [];
    public IList<MatchRow> TodayMatches { get; set; } = [];
    public IList<MatchRow> UpcomingMatches { get; set; } = [];
    public IList<MatchRow> LatestResults { get; set; } = [];
    public IList<CompetitionListItem> ActiveCompetitions { get; set; } = [];
    public IList<NewsListItem> LatestNews { get; set; } = [];
    public bool MatchesAreNational { get; set; }
}
