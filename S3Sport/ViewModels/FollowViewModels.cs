using S3Sport.Models;

namespace S3Sport.ViewModels;

/// <summary>State of the follow / notification controls for one target (team now; players, competitions, matches later).</summary>
public class FollowButtonViewModel
{
    public FollowTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool IsFollowing { get; set; }
    public bool NotificationsEnabled { get; set; }
    public int FollowersCount { get; set; }
    /// <summary>Compact = a single star (team cards); full = follow button + bell + count (detail pages).</summary>
    public bool Compact { get; set; }
}

public class FavoritesViewModel
{
    public IList<FavoriteTeamItem> Teams { get; set; } = [];
    public IList<FavoriteItem> Players { get; set; } = [];
    public IList<FavoriteItem> Competitions { get; set; } = [];
    public IList<MatchRow> Matches { get; set; } = [];
}

public class FavoriteTeamItem
{
    public int TeamId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? AreaText { get; set; }
    public DateTime FollowedAt { get; set; }
    public FollowButtonViewModel Follow { get; set; } = new();
}
