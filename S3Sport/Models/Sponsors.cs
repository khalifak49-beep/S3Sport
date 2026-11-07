using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

/// <summary>
/// A local sponsor shown on the public site. Scope decides where it appears:
/// the whole country, one governorate or wilayat (home page and that area's matches), or one competition
/// (its page and its matches). Admins manage sponsors; clicks and views are counted to report back to the sponsor.
/// </summary>
public class Sponsor : BaseEntity
{
    [Required, StringLength(120)] public string NameAr { get; set; } = string.Empty;
    /// <summary>Short line under the name, e.g. an offer for platform users.</summary>
    [StringLength(140)] public string? Tagline { get; set; }
    [Required, StringLength(300)] public string LogoPath { get; set; } = string.Empty;
    /// <summary>http(s) link opened on click; when empty, WhatsApp/phone is used.</summary>
    [StringLength(300)] public string? WebsiteUrl { get; set; }
    [StringLength(20)] public string? Phone { get; set; }

    public SponsorScope Scope { get; set; }
    public int? GovernorateId { get; set; }
    public Governorate? Governorate { get; set; }
    public int? WilayatId { get; set; }
    public Wilayat? Wilayat { get; set; }
    public int? CompetitionId { get; set; }
    public Competition? Competition { get; set; }

    /// <summary>Optional campaign window (Muscat dates, inclusive).</summary>
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public int Views { get; set; }
    public int Clicks { get; set; }
}

/// <summary>A photo from a match, uploaded by a manager of one of the two teams.</summary>
public class MatchPhoto : BaseEntity
{
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    /// <summary>The team whose manager uploaded it (also the team allowed to remove it).</summary>
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    [Required, StringLength(300)] public string Path { get; set; } = string.Empty;
    [StringLength(150)] public string? Caption { get; set; }
    public string? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedByUser { get; set; }
}
