namespace S3Sport.Models;

// Statistics are materialised per competition season so previous seasons stay intact.
// They are recalculated from finished matches / match events whenever a result changes.

public class CompetitionStanding : BaseEntity
{
    public int CompetitionSeasonId { get; set; }
    public CompetitionSeason CompetitionSeason { get; set; } = null!;
    public int? StageId { get; set; }
    public CompetitionStage? Stage { get; set; }
    public int? GroupId { get; set; }
    public CompetitionGroup? Group { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int Position { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    /// <summary>Computed column: GoalsFor - GoalsAgainst.</summary>
    public int GoalDifference { get; private set; }
    public int Points { get; set; }
    /// <summary>Manual deduction/bonus decided by the organiser (e.g. -3).</summary>
    public int PointsAdjustment { get; set; }
    /// <summary>Last five results, newest last, e.g. "WWDLW".</summary>
    public string? Form { get; set; }
}

public class PlayerStatistic : BaseEntity
{
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public int CompetitionSeasonId { get; set; }
    public CompetitionSeason CompetitionSeason { get; set; } = null!;
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int Appearances { get; set; }
    public int Starts { get; set; }
    public int Goals { get; set; }
    public int PenaltyGoals { get; set; }
    public int Assists { get; set; }
    public int OwnGoals { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int BestPlayerAwards { get; set; }
}

public class TeamStatistic : BaseEntity
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int CompetitionSeasonId { get; set; }
    public CompetitionSeason CompetitionSeason { get; set; } = null!;

    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int CleanSheets { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
}
