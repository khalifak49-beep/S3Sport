using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using S3Sport.Infrastructure;
using S3Sport.Models;

namespace S3Sport.ViewModels;

/// <summary>One "looking for a friendly" announcement as listed and shown.</summary>
public class FriendlyRequestCard
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamSlug { get; set; } = string.Empty;
    public string? TeamLogo { get; set; }
    public string? AreaText { get; set; }
    public DateTime PreferredDate { get; set; }
    public TimeSpan? PreferredTime { get; set; }
    public FriendlyVenuePreference VenuePreference { get; set; }
    public string? VenueName { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Notes { get; set; }
    public FriendlyRequestStatus Status { get; set; }
    public int? MatchId { get; set; }
    public int OfferCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FriendlyIndexViewModel
{
    public PagedResult<FriendlyRequestCard> Results { get; set; } = null!;
    public bool AllRegions { get; set; }
    public bool HasRegion { get; set; }
    public string? RegionName { get; set; }
    /// <summary>The signed-in user manages at least one approved team (may post and offer).</summary>
    public bool CanPost { get; set; }
    public bool IsSignedIn { get; set; }
}

public class FriendlyOfferRow
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamSlug { get; set; } = string.Empty;
    public string? TeamLogo { get; set; }
    public string? AreaText { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string? Message { get; set; }
    public FriendlyOfferStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FriendlyDetailsViewModel
{
    public FriendlyRequestCard Request { get; set; } = null!;
    public bool IsSignedIn { get; set; }
    /// <summary>The user manages the team that posted the request: sees and answers the offers.</summary>
    public bool IsOwner { get; set; }
    public List<FriendlyOfferRow> Offers { get; set; } = [];
    /// <summary>This user's teams that may still send an offer (approved, not the requesting team, no live offer yet).</summary>
    public List<SelectListItem> OfferTeams { get; set; } = [];
    /// <summary>Offers already sent by this user's teams.</summary>
    public List<FriendlyOfferRow> MyOffers { get; set; } = [];
    public string? DefaultPhone { get; set; }
    public bool IsOpen => Request.Status == FriendlyRequestStatus.Open && Request.PreferredDate >= DateTime.UtcNow.ToMuscat().Date;
}

public class FriendlyRequestFormViewModel
{
    [Display(Name = "الفريق")]
    public int TeamId { get; set; }

    [Display(Name = "اليوم المفضل"), Required(ErrorMessage = "اختر اليوم."), DataType(DataType.Date)]
    public DateTime? PreferredDate { get; set; }

    [Display(Name = "الوقت (اختياري)"), DataType(DataType.Time)]
    public TimeSpan? PreferredTime { get; set; }

    [Display(Name = "الملعب")]
    public FriendlyVenuePreference VenuePreference { get; set; } = FriendlyVenuePreference.Home;

    [Display(Name = "ملعبنا (اختياري)")]
    public int? VenueId { get; set; }

    [Display(Name = "رقم التواصل"), Required(ErrorMessage = "رقم التواصل مطلوب."), StringLength(30)]
    public string? ContactPhone { get; set; }

    [Display(Name = "اسم المنسق (اختياري)"), StringLength(100)]
    public string? ContactName { get; set; }

    [Display(Name = "ملاحظات (اختياري)"), StringLength(500)]
    public string? Notes { get; set; }

    public List<SelectListItem> Teams { get; set; } = [];
    public List<SelectListItem> Venues { get; set; } = [];
}

public class FriendlyMineViewModel
{
    public List<FriendlyRequestCard> Requests { get; set; } = [];
    public List<(FriendlyOfferRow Offer, FriendlyRequestCard Request)> Offers { get; set; } = [];
}
