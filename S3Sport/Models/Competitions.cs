using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

/// <summary>A sport (Football first). Sport-level defaults are copied into each competition season.</summary>
public class Sport : BaseEntity
{
    [Required, StringLength(80)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(80)] public string NameEn { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [StringLength(60)] public string? IconClass { get; set; }
    public int PlayersOnField { get; set; }
    public int PeriodsCount { get; set; } = 2;
    public int PeriodDurationMinutes { get; set; }
    public int PointsForWin { get; set; } = 3;
    public int PointsForDraw { get; set; } = 1;
    public int PointsForLoss { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<SportPosition> Positions { get; set; } = new List<SportPosition>();
}

public class SportPosition : BaseEntity
{
    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    [Required, StringLength(60)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(60)] public string NameEn { get; set; } = string.Empty;
    [Required, StringLength(10)] public string Code { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>A sporting season, e.g. "2025-2026". Shared by all competitions.</summary>
public class Season : BaseEntity
{
    [Required, StringLength(30)] public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class Venue : BaseEntity, ISoftDelete
{
    [Required, StringLength(150)] public string NameAr { get; set; } = string.Empty;
    [StringLength(150)] public string? NameEn { get; set; }
    public int? AreaId { get; set; }
    public Area? Area { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public int? Capacity { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

/// <summary>
/// A competition identity (e.g. "دوري ولاية السيب"). Its yearly editions are CompetitionSeasons.
/// Region scope: the deepest non-null region level; all null = national competition.
/// </summary>
public class Competition : BaseEntity, ISoftDelete
{
    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    [Required, StringLength(150)] public string NameAr { get; set; } = string.Empty;
    [StringLength(150)] public string? NameEn { get; set; }
    [Required, StringLength(170)] public string Slug { get; set; } = string.Empty;
    [StringLength(300)] public string? LogoPath { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(150)] public string? OrganizerName { get; set; }

    public int? GovernorateId { get; set; }
    public Governorate? Governorate { get; set; }
    public int? WilayatId { get; set; }
    public Wilayat? Wilayat { get; set; }
    public int? AreaId { get; set; }
    public Area? Area { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = null!;

    public ICollection<CompetitionSeason> Seasons { get; set; } = new List<CompetitionSeason>();
    public ICollection<CompetitionManager> Managers { get; set; } = new List<CompetitionManager>();
}

/// <summary>Users with the TournamentManager role who may manage a specific competition.</summary>
public class CompetitionManager
{
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
}

/// <summary>One edition of a competition in a season, holding its format and standings rules.</summary>
public class CompetitionSeason : BaseEntity
{
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public CompetitionFormat Format { get; set; }
    public CompetitionSeasonStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public int PointsForWin { get; set; } = 3;
    public int PointsForDraw { get; set; } = 1;
    public int PointsForLoss { get; set; }

    /// <summary>Comma-separated <see cref="TieBreakerCriterion"/> names, applied in order after points.</summary>
    [Required, StringLength(300)]
    public string TieBreakers { get; set; } = DefaultTieBreakers;

    [StringLength(4000)] public string? Regulations { get; set; }

    public int? ChampionTeamId { get; set; }
    public Team? ChampionTeam { get; set; }

    [Timestamp] public byte[] RowVersion { get; set; } = null!;

    public ICollection<CompetitionStage> Stages { get; set; } = new List<CompetitionStage>();
    public ICollection<CompetitionTeam> Teams { get; set; } = new List<CompetitionTeam>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public ICollection<CompetitionStanding> Standings { get; set; } = new List<CompetitionStanding>();

    public const string DefaultTieBreakers = "GoalDifference,GoalsFor,HeadToHeadPoints,Wins";

    public IReadOnlyList<TieBreakerCriterion> GetTieBreakers() =>
        TieBreakers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<TieBreakerCriterion>(s, out var c) ? (TieBreakerCriterion?)c : null)
            .Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();
}

public class CompetitionStage : BaseEntity
{
    public int CompetitionSeasonId { get; set; }
    public CompetitionSeason CompetitionSeason { get; set; } = null!;

    [Required, StringLength(100)] public string NameAr { get; set; } = string.Empty;
    public StageType StageType { get; set; }
    public int SortOrder { get; set; }
    /// <summary>1 = single match, 2 = home and away.</summary>
    public int Legs { get; set; } = 1;
    /// <summary>For group stages: how many teams qualify from each group.</summary>
    public int? QualifiersPerGroup { get; set; }

    public ICollection<CompetitionGroup> Groups { get; set; } = new List<CompetitionGroup>();
}

public class CompetitionGroup : BaseEntity
{
    public int StageId { get; set; }
    public CompetitionStage Stage { get; set; } = null!;
    [Required, StringLength(50)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<CompetitionTeam> Teams { get; set; } = new List<CompetitionTeam>();
}

public class CompetitionTeam : BaseEntity
{
    public int CompetitionSeasonId { get; set; }
    public CompetitionSeason CompetitionSeason { get; set; } = null!;
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int? GroupId { get; set; }
    public CompetitionGroup? Group { get; set; }
    public int? Seed { get; set; }
    public bool IsWithdrawn { get; set; }
}
