using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleRefresh roleRefresh) : Controller
{
    /// <summary>Roles only a SuperAdmin may grant or revoke.</summary>
    private static readonly string[] ProtectedRoles = [AppRoles.SuperAdmin, AppRoles.Admin];

    private bool IsSuperAdmin => User.IsInRole(AppRoles.SuperAdmin);
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? role, bool? active, int page = 1)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.FullName.Contains(s) || u.Email!.Contains(s) || u.PhoneNumber!.Contains(s));
        }
        if (!string.IsNullOrEmpty(role))
        {
            var roleId = await db.Roles.Where(r => r.Name == role).Select(r => r.Id).FirstOrDefaultAsync();
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }
        if (active.HasValue) query = query.Where(u => u.IsActive == active);

        var now = DateTimeOffset.UtcNow;
        var projected = query.OrderByDescending(u => u.CreatedAt).Select(u => new UserListItem
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email, PhoneNumber = u.PhoneNumber, IsActive = u.IsActive,
            IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now,
            CreatedAt = u.CreatedAt, LastLoginAt = u.LastLoginAt,
            Roles = db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!).ToList()
        });

        return View(new UserListViewModel
        {
            Results = await PagedResult<UserListItem>.CreateAsync(projected, page),
            Search = search, Role = role, Active = active
        });
    }

    [HttpGet]
    public async Task<IActionResult> Roles(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        return View(await BuildRolesModelAsync(user));
    }

    [HttpPost]
    public async Task<IActionResult> Roles(string id, List<string> selectedRoles)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var current = await userManager.GetRolesAsync(user);
        var wanted = selectedRoles.Where(AppRoles.All.Contains).Append(AppRoles.User).ToHashSet();

        // Non-SuperAdmins cannot touch protected roles: keep whatever the user currently has.
        if (!IsSuperAdmin)
        {
            foreach (var r in ProtectedRoles)
            {
                if (current.Contains(r)) wanted.Add(r); else wanted.Remove(r);
            }
        }
        // Nobody can remove their own SuperAdmin role (prevents locking the platform out).
        if (id == CurrentUserId && current.Contains(AppRoles.SuperAdmin)) wanted.Add(AppRoles.SuperAdmin);

        var toAdd = wanted.Except(current).ToList();
        var toRemove = current.Except(wanted).ToList();
        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);
        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);

        if (toAdd.Count + toRemove.Count > 0)
        {
            // Other sessions are invalidated (new stamp); the user's current session is re-issued with the new roles
            // on their next request instead of being signed out.
            await userManager.UpdateSecurityStampAsync(user);
            roleRefresh.MarkChanged(user.Id);
            db.AddAudit(HttpContext, "تعديل صلاحيات مستخدم", "User", user.Id,
                $"{user.Email} | +{string.Join(",", toAdd)} | -{string.Join(",", toRemove)}");
            await db.SaveChangesAsync();
        }

        TempData["Success"] = $"تم تحديث صلاحيات {user.FullName}.";
        return RedirectToAction(nameof(Roles), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (id == CurrentUserId)
        {
            TempData["Error"] = "لا يمكنك إيقاف حسابك.";
            return RedirectToAction(nameof(Index));
        }
        if (!IsSuperAdmin && (await userManager.GetRolesAsync(user)).Any(ProtectedRoles.Contains))
        {
            TempData["Error"] = "لا يمكنك إيقاف حساب مدير.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);
        // Signs the user out everywhere when deactivated.
        await userManager.UpdateSecurityStampAsync(user);

        db.AddAudit(HttpContext, user.IsActive ? "تفعيل مستخدم" : "إيقاف مستخدم", "User", user.Id, user.Email);
        await db.SaveChangesAsync();

        TempData["Success"] = user.IsActive ? $"تم تفعيل حساب {user.FullName}." : $"تم إيقاف حساب {user.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        db.AddAudit(HttpContext, "فك قفل مستخدم", "User", user.Id, user.Email);
        await db.SaveChangesAsync();
        TempData["Success"] = $"تم فك قفل حساب {user.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserRolesViewModel> BuildRolesModelAsync(ApplicationUser user)
    {
        var current = await userManager.GetRolesAsync(user);
        return new UserRolesViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = AppRoles.All.Select(r => new RoleCheckbox
            {
                Name = r,
                DisplayName = AppRoles.DisplayNames[r],
                Selected = current.Contains(r),
                Disabled = r == AppRoles.User
                    || (!IsSuperAdmin && ProtectedRoles.Contains(r))
                    || (user.Id == CurrentUserId && r == AppRoles.SuperAdmin)
            }).ToList(),
            ManagedTeams = await db.TeamMembers.Where(m => m.UserId == user.Id && m.IsActive)
                .Select(m => m.Team.NameAr).ToListAsync()
        };
    }
}
