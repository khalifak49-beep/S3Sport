using System.ComponentModel.DataAnnotations;
using S3Sport.Models;

namespace S3Sport.ViewModels;

/// <summary>Live state of a match — sent over SignalR and rendered by live.js on every page.</summary>
public class MatchLiveDto
{
    public int MatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int? HomePenaltyScore { get; set; }
    public int? AwayPenaltyScore { get; set; }
    public int Period { get; set; }
    public int PeriodDurationMinutes { get; set; }
    /// <summary>UTC ISO-8601; the client derives the running minute from it.</summary>
    public string? PeriodStartedAt { get; set; }
    public string KickoffAt { get; set; } = string.Empty;
    public List<LiveEventDto> Events { get; set; } = [];
}

public class LiveEventDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeText { get; set; } = string.Empty;
    /// <summary>"home", "away" or null for match-state events.</summary>
    public string? Side { get; set; }
    public int Minute { get; set; }
    public int? ExtraMinute { get; set; }
    public int Period { get; set; }
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public int? RelatedPlayerId { get; set; }
    public string? RelatedPlayerName { get; set; }
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; }
    public int RevisionCount { get; set; }
}

/// <summary>Operator input for a new event (JSON or form).</summary>
public class EventInput
{
    [Required] public MatchEventType? EventType { get; set; }
    public int? TeamId { get; set; }
    public int? PlayerId { get; set; }
    public int? RelatedPlayerId { get; set; }
    /// <summary>A typed shirt number or name, used when no registered player is picked.</summary>
    [StringLength(100)] public string? PlayerName { get; set; }
    [StringLength(100)] public string? RelatedPlayerName { get; set; }
    [Range(0, 200)] public int? Minute { get; set; }
    [Range(0, 30)] public int? ExtraMinute { get; set; }
    [StringLength(300)] public string? Notes { get; set; }
    /// <summary>Generated once per click on the client; repeated submits of the same click are ignored.</summary>
    [Required] public Guid? ClientRequestId { get; set; }
    // Only for MatchEnded in a drawn knockout match.
    [Range(0, 99)] public int? HomePenaltyScore { get; set; }
    [Range(0, 99)] public int? AwayPenaltyScore { get; set; }
}

/// <summary>Correction of an existing event (kept in MatchEventRevisions).</summary>
public class EventCorrectionInput
{
    public int? TeamId { get; set; }
    public int? PlayerId { get; set; }
    public int? RelatedPlayerId { get; set; }
    [StringLength(100)] public string? PlayerName { get; set; }
    [StringLength(100)] public string? RelatedPlayerName { get; set; }
    [Range(0, 200)] public int Minute { get; set; }
    [Range(0, 30)] public int? ExtraMinute { get; set; }
    [StringLength(300)] public string? Notes { get; set; }
    [Required(ErrorMessage = "سبب التصحيح مطلوب"), StringLength(500)] public string? Reason { get; set; }
}

public class SquadPlayer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? Position { get; set; }
    public bool InLineup { get; set; }
    public bool IsStarter { get; set; }
}

public class OperatorViewModel
{
    public int MatchId { get; set; }
    public string HomeName { get; set; } = string.Empty;
    public string? HomeLogo { get; set; }
    public int HomeTeamId { get; set; }
    public string AwayName { get; set; } = string.Empty;
    public string? AwayLogo { get; set; }
    public int AwayTeamId { get; set; }
    public string? CompetitionName { get; set; }
    public string? RoundName { get; set; }
    public string? VenueName { get; set; }
    public bool IsKnockout { get; set; }
    /// <summary>Guest teams have no squad: the operator types player names.</summary>
    public bool HomeIsGuest { get; set; }
    public bool AwayIsGuest { get; set; }
    public MatchStatus Status { get; set; }
    public MatchLiveDto Live { get; set; } = new();
    public List<SquadPlayer> HomeSquad { get; set; } = [];
    public List<SquadPlayer> AwaySquad { get; set; } = [];
    public int? BestPlayerId { get; set; }
    public int? Attendance { get; set; }
    public string? Summary { get; set; }
    public List<RevisionRow> Revisions { get; set; } = [];
}

public class RevisionRow
{
    public int EventId { get; set; }
    public EventRevisionAction Action { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}

public class OperatorMatchListViewModel
{
    public List<MatchRow> Live { get; set; } = [];
    public List<MatchRow> Upcoming { get; set; } = [];
    public List<MatchRow> Recent { get; set; } = [];
}
