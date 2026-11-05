using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

public class News : BaseEntity, ISoftDelete
{
    [Required, StringLength(250)] public string TitleAr { get; set; } = string.Empty;
    [Required, StringLength(270)] public string Slug { get; set; } = string.Empty;
    [StringLength(500)] public string? Summary { get; set; }
    [Required] public string Body { get; set; } = string.Empty;
    [StringLength(300)] public string? CoverImagePath { get; set; }
    [StringLength(300)] public string? VideoUrl { get; set; }
    public NewsCategory Category { get; set; }

    // Optional links: a news item can be tied to a team, player, competition and/or match.
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
    public int? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    public int? MatchId { get; set; }
    public Match? Match { get; set; }

    public string? AuthorUserId { get; set; }
    public ApplicationUser? AuthorUser { get; set; }

    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = null!;

    public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();
}

/// <summary>Uploaded image or linked video (gallery items for news, teams, players, matches).</summary>
public class MediaFile : BaseEntity, ISoftDelete
{
    public MediaType MediaType { get; set; }
    /// <summary>Relative web path under /uploads, or an external video URL.</summary>
    [Required, StringLength(500)] public string FilePath { get; set; } = string.Empty;
    [StringLength(260)] public string? OriginalFileName { get; set; }
    [StringLength(100)] public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    [StringLength(300)] public string? Caption { get; set; }

    public int? NewsId { get; set; }
    public News? News { get; set; }
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
    public int? MatchId { get; set; }
    public Match? Match { get; set; }

    public string? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedByUser { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
