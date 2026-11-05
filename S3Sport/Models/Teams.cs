using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

public class Team : BaseEntity, ISoftDelete
{
    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    [Required, StringLength(150)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(150)] public string NameEn { get; set; } = string.Empty;
    [Required, StringLength(170)] public string Slug { get; set; } = string.Empty;
    [StringLength(30)] public string? ShortName { get; set; }
    [StringLength(300)] public string? LogoPath { get; set; }
    public int? FoundedYear { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(30)] public string? ContactPhone { get; set; }
    [StringLength(150)] public string? ContactEmail { get; set; }
    [StringLength(300)] public string? SocialUrl { get; set; }
    [StringLength(9)] public string? PrimaryColor { get; set; }

    public TeamStatus Status { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public int? VillageId { get; set; }
    public Village? Village { get; set; }
    public int? HomeVenueId { get; set; }
    public Venue? HomeVenue { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = null!;

    public ICollection<TeamArea> TeamAreas { get; set; } = new List<TeamArea>();
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    public ICollection<TeamPlayer> TeamPlayers { get; set; } = new List<TeamPlayer>();
    public ICollection<TeamAchievement> Achievements { get; set; } = new List<TeamAchievement>();
    public ICollection<CompetitionTeam> Competitions { get; set; } = new List<CompetitionTeam>();
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}

/// <summary>Areas a team belongs to (a team may appear in more than one). One row is primary.</summary>
public class TeamArea
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int AreaId { get; set; }
    public Area Area { get; set; } = null!;
    public bool IsPrimary { get; set; }
}

/// <summary>Users who manage a team. A TeamManager can only manage teams they are an active member of.</summary>
public class TeamMember : BaseEntity
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public TeamMemberRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TeamApplication : BaseEntity
{
    [Required] public string ApplicantUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicantUser { get; set; } = null!;

    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    [Required, StringLength(150)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(150)] public string NameEn { get; set; } = string.Empty;
    [StringLength(300)] public string? LogoPath { get; set; }

    public int GovernorateId { get; set; }
    public Governorate Governorate { get; set; } = null!;
    public int WilayatId { get; set; }
    public Wilayat Wilayat { get; set; } = null!;
    /// <summary>Null when the applicant's area is not registered yet (see ProposedAreaName).</summary>
    public int? AreaId { get; set; }
    public Area? Area { get; set; }
    /// <summary>
    /// Area name typed by the applicant when it is missing from the list. On approval the area is created
    /// under WilayatId (or matched by name) and AreaId is set; the proposed name is kept for history.
    /// </summary>
    [StringLength(100)] public string? ProposedAreaName { get; set; }
    public int? VillageId { get; set; }
    public Village? Village { get; set; }

    public int? FoundedYear { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [Required, StringLength(30)] public string ContactPhone { get; set; } = string.Empty;
    [StringLength(150)] public string? ContactEmail { get; set; }

    public TeamStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [StringLength(1000)] public string? ReviewNotes { get; set; }

    public int? CreatedTeamId { get; set; }
    public Team? CreatedTeam { get; set; }

    [Timestamp] public byte[] RowVersion { get; set; } = null!;

    /// <summary>Extra areas the team should also appear in (besides AreaId).</summary>
    public ICollection<TeamApplicationArea> AdditionalAreas { get; set; } = new List<TeamApplicationArea>();
}

public class TeamApplicationArea
{
    public int TeamApplicationId { get; set; }
    public TeamApplication TeamApplication { get; set; } = null!;
    public int AreaId { get; set; }
    public Area Area { get; set; } = null!;
}

public class TeamAchievement : BaseEntity
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    [Required, StringLength(200)] public string TitleAr { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
}
