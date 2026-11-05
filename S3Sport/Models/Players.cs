using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

/// <summary>
/// A player. The current team is the active TeamPlayer row; career totals come from
/// PlayerStatistics (per competition season) so history survives transfers.
/// </summary>
public class Player : BaseEntity, ISoftDelete
{
    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    [Required, StringLength(150)] public string FullNameAr { get; set; } = string.Empty;
    [StringLength(150)] public string? FullNameEn { get; set; }
    [StringLength(300)] public string? PhotoPath { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [StringLength(60)] public string? Nationality { get; set; }
    public int? PositionId { get; set; }
    public SportPosition? Position { get; set; }
    [StringLength(1000)] public string? Bio { get; set; }

    /// <summary>Optional link when the player has their own account.</summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = null!;

    public ICollection<TeamPlayer> TeamPlayers { get; set; } = new List<TeamPlayer>();
    public ICollection<PlayerTransfer> Transfers { get; set; } = new List<PlayerTransfer>();
    public ICollection<PlayerStatistic> Statistics { get; set; } = new List<PlayerStatistic>();
}

/// <summary>Player membership in a team over time. At most one active row per player.</summary>
public class TeamPlayer : BaseEntity
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public int? ShirtNumber { get; set; }
    public int? PositionId { get; set; }
    public SportPosition? Position { get; set; }
    public DateOnly JoinedOn { get; set; }
    public DateOnly? LeftOn { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsCaptain { get; set; }
}

public class PlayerTransfer : BaseEntity
{
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public int? FromTeamId { get; set; }
    public Team? FromTeam { get; set; }
    /// <summary>Null when the player was released without joining a new team.</summary>
    public int? ToTeamId { get; set; }
    public Team? ToTeam { get; set; }
    public DateOnly TransferDate { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    public string? RecordedByUserId { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
}
