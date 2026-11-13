using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Hubs;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>
/// "Looking for a friendly" board. A team manager posts that their team wants a friendly (day, home/away, contact number).
/// Anyone sees the board; signed-in users see the number (call / WhatsApp). Managers of other approved teams can also send
/// an offer through the platform; the requesting team accepts one, which creates the friendly on both teams' schedules.
/// </summary>
public class FriendliesController(ApplicationDbContext db, IHubContext<LiveHub> hub, ILogger<FriendliesController> logger) : Controller
{
    private const int MaxOpenPerTeam = 3;
    private static readonly TimeSpan DefaultKickoff = TimeSpan.FromHours(17);

    private string? UserId => User.GetUserId();
    private static DateTime Today => DateTime.UtcNow.ToMuscat().Date;

    private static readonly Expression<Func<FriendlyRequest, FriendlyRequestCard>> ToCard = r => new FriendlyRequestCard
    {
        Id = r.Id, TeamId = r.TeamId, TeamName = r.Team.NameAr, TeamSlug = r.Team.Slug, TeamLogo = r.Team.LogoPath,
        AreaText = r.Team.TeamAreas.Where(ta => ta.IsPrimary).Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr).FirstOrDefault(),
        PreferredDate = r.PreferredDate, PreferredTime = r.PreferredTime, VenuePreference = r.VenuePreference,
        VenueName = r.Venue != null ? r.Venue.NameAr : null, ContactPhone = r.ContactPhone, ContactName = r.ContactName,
        Notes = r.Notes, Status = r.Status, MatchId = r.MatchId,
        OfferCount = r.Offers.Count(o => o.Status == FriendlyOfferStatus.Pending), CreatedAt = r.CreatedAt
    };

    private static readonly Expression<Func<FriendlyOffer, FriendlyOfferRow>> ToOfferRow = o => new FriendlyOfferRow
    {
        Id = o.Id, TeamId = o.TeamId, TeamName = o.Team.NameAr, TeamSlug = o.Team.Slug, TeamLogo = o.Team.LogoPath,
        AreaText = o.Team.TeamAreas.Where(ta => ta.IsPrimary).Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr).FirstOrDefault(),
        ContactPhone = o.ContactPhone, Message = o.Message, Status = o.Status, CreatedAt = o.CreatedAt
    };

    /// <summary>Approved teams the signed-in user may act for (admins: none here; they use a team manager account).</summary>
    private IQueryable<Team> MyApprovedTeams()
    {
        var uid = UserId;
        var isManager = User.IsInRole(AppRoles.TeamManager);
        return db.Teams.Where(t => isManager && t.Status == TeamStatus.Approved
                                   && t.Members.Any(m => m.UserId == uid && m.IsActive && m.Role == TeamMemberRole.Manager));
    }

    // ---------------- public board ----------------

    [HttpGet]
    public async Task<IActionResult> Index(bool all = false, int page = 1)
    {
        var region = CurrentRegion.FromRequest(Request);
        var allRegions = all || region == null;
        var today = Today;
        var open = db.FriendlyRequests.AsNoTracking()
            .Where(r => r.Status == FriendlyRequestStatus.Open && r.PreferredDate >= today && r.Team.Status == TeamStatus.Approved);
        if (!allRegions)
        {
            var regionTeams = db.Teams.InRegion(region).Select(t => t.Id);
            open = open.Where(r => regionTeams.Contains(r.TeamId));
        }

        return View(new FriendlyIndexViewModel
        {
            Results = await PagedResult<FriendlyRequestCard>.CreateAsync(open.OrderBy(r => r.PreferredDate).ThenBy(r => r.Id).Select(ToCard), page, 24),
            AllRegions = allRegions, HasRegion = region != null,
            RegionName = region == null ? null : await region.GetDisplayNameAsync(db),
            IsSignedIn = User.Identity?.IsAuthenticated == true,
            CanPost = UserId != null && await MyApprovedTeams().AnyAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var card = await db.FriendlyRequests.AsNoTracking().Where(r => r.Id == id && r.Team.Status == TeamStatus.Approved)
            .Select(ToCard).FirstOrDefaultAsync();
        if (card == null) return NotFound();

        var model = new FriendlyDetailsViewModel { Request = card, IsSignedIn = User.Identity?.IsAuthenticated == true };
        if (UserId != null)
        {
            model.IsOwner = await db.CanManageTeamAsync(User, card.TeamId);
            if (model.IsOwner)
                model.Offers = await db.FriendlyOffers.AsNoTracking().Where(o => o.FriendlyRequestId == id && o.Status != FriendlyOfferStatus.Withdrawn)
                    .OrderBy(o => o.Status).ThenBy(o => o.CreatedAt).Select(ToOfferRow).ToListAsync();

            var mine = MyApprovedTeams().Where(t => t.Id != card.TeamId);
            model.MyOffers = await db.FriendlyOffers.AsNoTracking()
                .Where(o => o.FriendlyRequestId == id && mine.Any(t => t.Id == o.TeamId)).Select(ToOfferRow).ToListAsync();
            var live = model.MyOffers.Where(o => o.Status is FriendlyOfferStatus.Pending or FriendlyOfferStatus.Accepted).Select(o => o.TeamId).ToList();
            model.OfferTeams = await mine.Where(t => !live.Contains(t.Id)).OrderBy(t => t.NameAr)
                .Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync();
            model.DefaultPhone = await DefaultPhoneAsync(model.OfferTeams.Count == 1 ? int.Parse(model.OfferTeams[0].Value) : null);
        }
        return View(model);
    }

    // ---------------- the requesting team ----------------

    [HttpGet, Authorize(Policy = AppPolicies.ManageTeams)]
    public async Task<IActionResult> Mine()
    {
        var teams = MyApprovedTeams().Select(t => t.Id);
        var model = new FriendlyMineViewModel
        {
            Requests = await db.FriendlyRequests.AsNoTracking().Where(r => teams.Contains(r.TeamId))
                .OrderByDescending(r => r.Status == FriendlyRequestStatus.Open).ThenByDescending(r => r.PreferredDate).Take(30).Select(ToCard).ToListAsync()
        };
        var offers = await db.FriendlyOffers.AsNoTracking().Where(o => teams.Contains(o.TeamId)).OrderByDescending(o => o.CreatedAt).Take(30)
            .Select(o => new { Row = new FriendlyOfferRow
            {
                Id = o.Id, TeamId = o.TeamId, TeamName = o.Team.NameAr, TeamSlug = o.Team.Slug, TeamLogo = o.Team.LogoPath,
                ContactPhone = o.ContactPhone, Message = o.Message, Status = o.Status, CreatedAt = o.CreatedAt
            }, o.FriendlyRequestId }).ToListAsync();
        var ids = offers.Select(o => o.FriendlyRequestId).Distinct().ToList();
        var requests = await db.FriendlyRequests.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(ToCard).ToDictionaryAsync(r => r.Id);
        model.Offers = offers.Where(o => requests.ContainsKey(o.FriendlyRequestId)).Select(o => (o.Row, requests[o.FriendlyRequestId])).ToList();
        return View(model);
    }

    [HttpGet, Authorize(Policy = AppPolicies.ManageTeams)]
    public async Task<IActionResult> Create(int? teamId)
    {
        var teams = await MyApprovedTeams().OrderBy(t => t.NameAr).Select(t => new { t.Id, t.NameAr, t.HomeVenueId }).ToListAsync();
        if (teams.Count == 0)
        {
            TempData["Error"] = "نشر طلب مباراة ودية متاح لمسؤولي الفرق المعتمدة.";
            return RedirectToAction(nameof(Index));
        }
        var team = teams.FirstOrDefault(t => t.Id == teamId) ?? teams[0];
        var model = new FriendlyRequestFormViewModel
        {
            TeamId = team.Id, PreferredDate = Today.AddDays(1), VenueId = team.HomeVenueId,
            ContactPhone = await DefaultPhoneAsync(team.Id)
        };
        return View(await FillFormAsync(model));
    }

    [HttpPost, Authorize(Policy = AppPolicies.ManageTeams), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FriendlyRequestFormViewModel model)
    {
        if (!await MyApprovedTeams().AnyAsync(t => t.Id == model.TeamId))
            ModelState.AddModelError(nameof(model.TeamId), "اختر فريقاً معتمداً تديره.");
        var phone = PhoneHelper.Normalize(model.ContactPhone);
        if (phone == null) ModelState.AddModelError(nameof(model.ContactPhone), "اكتب رقماً صحيحاً (8 إلى 15 رقماً).");
        if (model.PreferredDate is { } d && (d.Date < Today || d.Date > Today.AddDays(180)))
            ModelState.AddModelError(nameof(model.PreferredDate), "اختر يوماً من اليوم وحتى 6 أشهر قادمة.");
        if (!Enum.IsDefined(model.VenuePreference)) ModelState.AddModelError(nameof(model.VenuePreference), "اختر الملعب.");
        if (model.VenueId != null && !await db.Venues.AnyAsync(v => v.Id == model.VenueId)) ModelState.AddModelError(nameof(model.VenueId), "الملعب غير صالح.");
        if (await db.FriendlyRequests.CountAsync(r => r.TeamId == model.TeamId && r.Status == FriendlyRequestStatus.Open && r.PreferredDate >= Today) >= MaxOpenPerTeam)
            ModelState.AddModelError(string.Empty, $"لفريقك {MaxOpenPerTeam} طلبات مفتوحة. أغلق طلباً قديماً أولاً.");
        if (!ModelState.IsValid) return View(await FillFormAsync(model));

        var request = new FriendlyRequest
        {
            TeamId = model.TeamId, PreferredDate = model.PreferredDate!.Value.Date, PreferredTime = model.PreferredTime,
            VenuePreference = model.VenuePreference, VenueId = model.VenuePreference == FriendlyVenuePreference.Away ? null : model.VenueId,
            ContactPhone = phone!, ContactName = Clean(model.ContactName), Notes = Clean(model.Notes),
            Status = FriendlyRequestStatus.Open, CreatedByUserId = UserId!
        };
        db.FriendlyRequests.Add(request);
        await db.SaveChangesAsync();
        db.AddAudit(HttpContext, "طلب مباراة ودية", nameof(Team), model.TeamId, $"{request.PreferredDate:yyyy/MM/dd} — {request.VenuePreference.GetDisplayName()}");
        await db.SaveChangesAsync();
        TempData["Success"] = "تم نشر طلبك. يظهر الآن في صفحة «مباريات ودية» ويستطيع أي فريق التواصل معك أو إرسال عرض.";
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [HttpPost, Authorize(Policy = AppPolicies.ManageTeams), ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var request = await db.FriendlyRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (!await db.CanManageTeamAsync(User, request.TeamId)) return Forbid();
        if (request.Status != FriendlyRequestStatus.Open) return RedirectToAction(nameof(Details), new { id });

        var pending = await db.FriendlyOffers.Where(o => o.FriendlyRequestId == id && o.Status == FriendlyOfferStatus.Pending).ToListAsync();
        request.Status = FriendlyRequestStatus.Closed;
        pending.ForEach(o => o.Status = FriendlyOfferStatus.Declined);
        db.AddAudit(HttpContext, "إغلاق طلب مباراة ودية", nameof(Team), request.TeamId, $"طلب {id}");
        await db.SaveChangesAsync();
        await NotifySafeAsync(pending.Select(o => o.CreatedByUserId), NotificationType.FriendlyReply,
            $"أُغلق طلب المباراة الودية: {await TeamNameAsync(request.TeamId)}", "لم يعد الفريق يبحث عن مباراة في هذا الموعد.", id);
        TempData["Success"] = "تم إغلاق الطلب.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost, Authorize(Policy = AppPolicies.ManageTeams), ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int offerId)
    {
        var offer = await db.FriendlyOffers.Include(o => o.FriendlyRequest).FirstOrDefaultAsync(o => o.Id == offerId);
        if (offer == null) return NotFound();
        var request = offer.FriendlyRequest;
        if (!await db.CanManageTeamAsync(User, request.TeamId)) return Forbid();
        if (offer.Status != FriendlyOfferStatus.Pending || request.Status != FriendlyRequestStatus.Open)
        {
            TempData["Error"] = "لا يمكن قبول هذا العرض الآن.";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }
        if (!await db.Teams.AnyAsync(t => t.Id == offer.TeamId && t.Status == TeamStatus.Approved))
        {
            TempData["Error"] = "الفريق صاحب العرض لم يعد معتمداً.";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }

        Match? match = null;
        List<string> declinedUsers = [];
        var taken = false;
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            // Only one offer can win: the request flips from Open to Matched exactly once.
            var flipped = await db.FriendlyRequests.Where(r => r.Id == request.Id && r.Status == FriendlyRequestStatus.Open)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, FriendlyRequestStatus.Matched).SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
            if (flipped == 0) { taken = true; return; }

            var ourHome = request.VenuePreference != FriendlyVenuePreference.Away;
            match = new Match
            {
                SportId = SeedData.FootballSportId, OrganizerTeamId = request.TeamId,
                HomeTeamId = ourHome ? request.TeamId : offer.TeamId, AwayTeamId = ourHome ? offer.TeamId : request.TeamId,
                KickoffAt = request.PreferredDate.Date.Add(request.PreferredTime ?? DefaultKickoff).FromMuscat(),
                VenueId = ourHome ? request.VenueId : null, Status = MatchStatus.Scheduled,
                Notes = "مباراة ودية تم الاتفاق عليها عبر صفحة المباريات الودية."
            };
            db.Matches.Add(match);
            await db.SaveChangesAsync();
            await db.FriendlyRequests.Where(r => r.Id == request.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.MatchId, match.Id));
            await db.FriendlyOffers.Where(o => o.Id == offer.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, FriendlyOfferStatus.Accepted));
            declinedUsers = await db.FriendlyOffers.Where(o => o.FriendlyRequestId == request.Id && o.Id != offer.Id && o.Status == FriendlyOfferStatus.Pending)
                .Select(o => o.CreatedByUserId).ToListAsync();
            await db.FriendlyOffers.Where(o => o.FriendlyRequestId == request.Id && o.Id != offer.Id && o.Status == FriendlyOfferStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, FriendlyOfferStatus.Declined));
            db.AddAudit(HttpContext, "قبول عرض مباراة ودية", nameof(Match), match.Id, $"طلب {request.Id}، عرض {offer.Id}");
            await db.SaveChangesAsync();
        });
        if (taken)
        {
            TempData["Error"] = "تم الاتفاق على هذا الطلب مسبقاً.";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }

        var requester = await TeamNameAsync(request.TeamId);
        var when = match!.KickoffAt.ToMuscatString("dddd yyyy/MM/dd HH:mm");
        var offerManagers = await NotificationHelper.TeamManagerIdsAsync(db, offer.TeamId);
        await NotifySafeAsync(offerManagers.Append(offer.CreatedByUserId), NotificationType.FriendlyReply,
            $"✅ قبل {requester} عرضكم للمباراة الودية", $"{when} — تجدونها في مباريات فريقكم.", request.Id, $"/matches/{match.Id}");
        await NotifySafeAsync(declinedUsers, NotificationType.FriendlyReply,
            $"اتفق {requester} مع فريق آخر", "شكراً لعرضكم. تابعوا الطلبات الأخرى في صفحة المباريات الودية.", request.Id);
        try
        {
            var title = await db.Matches.Where(m => m.Id == match.Id).Select(m => m.HomeTeam.NameAr + " × " + m.AwayTeam.NameAr).FirstAsync();
            await NotificationHelper.NotifyMatchFollowersAsync(db, hub, match.Id,
                NotificationType.MatchScheduled, $"مباراة ودية جديدة: {title}", when);
            await hub.Clients.Group(LiveHub.LiveGroup).SendAsync("matchChanged", new { matchId = match.Id });
        }
        catch (Exception ex) { logger.LogWarning(ex, "Follower notification failed for friendly {MatchId}", match.Id); }

        TempData["Success"] = "تم الاتفاق! أُضيفت المباراة إلى مباريات الفريقين، ويمكنك تعديل موعدها وتسجيل أحداثها من «مباريات الفريق».";
        return RedirectToAction("Matches", "MyTeams", new { id = request.TeamId });
    }

    [HttpPost, Authorize(Policy = AppPolicies.ManageTeams), ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(int offerId)
    {
        var offer = await db.FriendlyOffers.Include(o => o.FriendlyRequest).FirstOrDefaultAsync(o => o.Id == offerId);
        if (offer == null) return NotFound();
        if (!await db.CanManageTeamAsync(User, offer.FriendlyRequest.TeamId)) return Forbid();
        if (offer.Status == FriendlyOfferStatus.Pending)
        {
            offer.Status = FriendlyOfferStatus.Declined;
            await db.SaveChangesAsync();
            await NotifySafeAsync([offer.CreatedByUserId], NotificationType.FriendlyReply,
                $"اعتذر {await TeamNameAsync(offer.FriendlyRequest.TeamId)} عن عرضكم للمباراة الودية", null, offer.FriendlyRequestId);
            TempData["Success"] = "تم رفض العرض.";
        }
        return RedirectToAction(nameof(Details), new { id = offer.FriendlyRequestId });
    }

    // ---------------- other teams ----------------

    [HttpPost, Authorize(Policy = AppPolicies.ManageTeams), ValidateAntiForgeryToken]
    public async Task<IActionResult> Offer(int id, int teamId, string? contactPhone, string? message)
    {
        var request = await db.FriendlyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        string? error = null;
        var phone = PhoneHelper.Normalize(contactPhone);
        if (!await MyApprovedTeams().AnyAsync(t => t.Id == teamId)) error = "اختر فريقاً معتمداً تديره.";
        else if (teamId == request.TeamId) error = "لا يمكن إرسال عرض لطلب فريقك.";
        else if (request.Status != FriendlyRequestStatus.Open || request.PreferredDate < Today) error = "هذا الطلب لم يعد مفتوحاً.";
        else if (phone == null) error = "اكتب رقم تواصل صحيحاً (8 إلى 15 رقماً).";
        else if (message?.Trim().Length > 500) error = "الرسالة طويلة (500 حرف كحد أقصى).";
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        var offer = await db.FriendlyOffers.FirstOrDefaultAsync(o => o.FriendlyRequestId == id && o.TeamId == teamId);
        if (offer is { Status: FriendlyOfferStatus.Pending or FriendlyOfferStatus.Accepted })
        {
            TempData["Error"] = "أرسل فريقك عرضاً لهذا الطلب مسبقاً.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (offer == null)
        {
            offer = new FriendlyOffer { FriendlyRequestId = id, TeamId = teamId };
            db.FriendlyOffers.Add(offer);
        }
        offer.ContactPhone = phone!;
        offer.Message = Clean(message);
        offer.Status = FriendlyOfferStatus.Pending;
        offer.CreatedByUserId = UserId!;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            TempData["Error"] = "أرسل فريقك عرضاً لهذا الطلب مسبقاً.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var from = await TeamNameAsync(teamId);
        await NotifySafeAsync(await NotificationHelper.TeamManagerIdsAsync(db, request.TeamId), NotificationType.FriendlyOffer,
            $"🤝 عرض مباراة ودية من {from}", (offer.Message ?? "يرغب في اللعب معكم") + $" — {request.PreferredDate:yyyy/MM/dd}", id);
        TempData["Success"] = "تم إرسال العرض. سيصلك إشعار عند رد الفريق، ويمكنك أيضاً التواصل معه مباشرة.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Policy = AppPolicies.ManageTeams), ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int offerId)
    {
        var offer = await db.FriendlyOffers.FirstOrDefaultAsync(o => o.Id == offerId);
        if (offer == null) return NotFound();
        if (!await db.CanManageTeamAsync(User, offer.TeamId)) return Forbid();
        if (offer.Status == FriendlyOfferStatus.Pending)
        {
            offer.Status = FriendlyOfferStatus.Withdrawn;
            await db.SaveChangesAsync();
            TempData["Success"] = "تم سحب العرض.";
        }
        return RedirectToAction(nameof(Details), new { id = offer.FriendlyRequestId });
    }

    // ---------------- helpers ----------------

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private Task<string> TeamNameAsync(int teamId) => db.Teams.Where(t => t.Id == teamId).Select(t => t.NameAr).FirstAsync();

    /// <summary>The team's contact number, else the user's own phone number.</summary>
    private async Task<string?> DefaultPhoneAsync(int? teamId)
    {
        var teamPhone = teamId == null ? null : await db.Teams.Where(t => t.Id == teamId).Select(t => t.ContactPhone).FirstOrDefaultAsync();
        return teamPhone ?? await db.Users.Where(u => u.Id == UserId).Select(u => u.PhoneNumber).FirstOrDefaultAsync();
    }

    private async Task<FriendlyRequestFormViewModel> FillFormAsync(FriendlyRequestFormViewModel model)
    {
        model.Teams = await MyApprovedTeams().OrderBy(t => t.NameAr).Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync();
        model.Venues = await db.Venues.AsNoTracking().OrderBy(v => v.NameAr).Select(v => new SelectListItem(v.NameAr, v.Id.ToString())).ToListAsync();
        return model;
    }

    /// <summary>The change is committed; a notification failure must not become an error for the manager.</summary>
    private async Task NotifySafeAsync(IEnumerable<string> userIds, NotificationType type, string title, string? message, int requestId, string? url = null)
    {
        try
        {
            var ids = userIds.Where(u => u != UserId).Distinct().ToList();
            await NotificationHelper.NotifyUsersAsync(db, hub, ids, type, title, message, url ?? $"/Friendlies/Details/{requestId}");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Friendly notification failed for request {RequestId}", requestId); }
    }
}
