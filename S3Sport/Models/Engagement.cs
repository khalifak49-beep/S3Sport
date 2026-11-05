using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

/// <summary>
/// A followed item. Uses one nullable FK per target type (instead of a polymorphic id)
/// so every follow keeps referential integrity; a check constraint enforces exactly one target.
/// </summary>
public class UserFollow : BaseEntity
{
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public FollowTargetType TargetType { get; set; }

    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
    public int? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    public int? MatchId { get; set; }
    public Match? Match { get; set; }

    public bool NotificationsEnabled { get; set; } = true;
}

public class Notification : BaseEntity
{
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public NotificationType Type { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(500)] public string? Message { get; set; }
    [StringLength(300)] public string? Url { get; set; }

    public int? MatchId { get; set; }
    public Match? Match { get; set; }
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public int? NewsId { get; set; }
    public News? News { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>Outbox flag: still to be sent to the user's devices (Web Push). Cleared by PushWorker.</summary>
    public bool PushPending { get; set; } = true;
}

/// <summary>Browser push subscription (Web Push / VAPID): one row per device/browser, removed when the push service reports it gone.</summary>
public class PushSubscription : BaseEntity
{
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [Required, StringLength(800)] public string Endpoint { get; set; } = string.Empty;
    [Required, StringLength(200)] public string P256dh { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Auth { get; set; } = string.Empty;
    [StringLength(300)] public string? UserAgent { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    [StringLength(256)] public string? UserName { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(100)] public string EntityName { get; set; } = string.Empty;
    [StringLength(50)] public string? EntityId { get; set; }
    public string? Details { get; set; }
    [StringLength(64)] public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
