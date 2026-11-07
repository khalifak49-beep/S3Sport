using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

/// <summary>
/// A team announces it is looking for a friendly ("فريق الشباب يبحث عن مباراة ودية في ملعبه").
/// Other teams call the contact number or send an offer through the platform; accepting an offer creates the friendly.
/// </summary>
public class FriendlyRequest : BaseEntity
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>Preferred day (Muscat calendar date, time part is midnight).</summary>
    public DateTime PreferredDate { get; set; }
    /// <summary>Preferred kick-off time of day (Muscat), if the team has one.</summary>
    public TimeSpan? PreferredTime { get; set; }
    public FriendlyVenuePreference VenuePreference { get; set; }
    public int? VenueId { get; set; }
    public Venue? Venue { get; set; }

    [Required, StringLength(30)] public string ContactPhone { get; set; } = string.Empty;
    [StringLength(100)] public string? ContactName { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    public FriendlyRequestStatus Status { get; set; }
    /// <summary>The friendly created when an offer was accepted.</summary>
    public int? MatchId { get; set; }
    public Match? Match { get; set; }

    [Required] public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<FriendlyOffer> Offers { get; set; } = new List<FriendlyOffer>();
}

/// <summary>Another team's answer to a friendly request. One per team and request.</summary>
public class FriendlyOffer : BaseEntity
{
    public int FriendlyRequestId { get; set; }
    public FriendlyRequest FriendlyRequest { get; set; } = null!;
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    [Required, StringLength(30)] public string ContactPhone { get; set; } = string.Empty;
    [StringLength(500)] public string? Message { get; set; }
    public FriendlyOfferStatus Status { get; set; }

    [Required] public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
