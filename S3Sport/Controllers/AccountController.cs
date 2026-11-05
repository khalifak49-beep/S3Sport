using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

public class AccountController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturnUrl(returnUrl));
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is { IsActive: false })
        {
            ModelState.AddModelError(string.Empty, "هذا الحساب موقوف. يرجى التواصل مع إدارة المنصة.");
            return View(model);
        }

        var result = user == null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "تم قفل الحساب مؤقتاً بسبب محاولات دخول خاطئة متكررة. حاول بعد 15 دقيقة.");
            return View(model);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
            return View(model);
        }

        user!.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        await RestoreDefaultRegionAsync(user.Id);
        logger.LogInformation("User {Email} signed in.", user.Email);

        return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await userManager.AddToRoleAsync(user, AppRoles.User);

        // A guest who already picked a region keeps it as their default.
        var region = CurrentRegion.FromRequest(Request);
        if (region != null)
        {
            db.UserAreas.Add(new UserArea
            {
                UserId = user.Id, GovernorateId = region.GovernorateId, WilayatId = region.WilayatId,
                AreaId = region.AreaId, VillageId = region.VillageId, IsDefault = true
            });
            await db.SaveChangesAsync();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        logger.LogInformation("New user registered: {Email}", user.Email);
        TempData["Success"] = $"مرحباً {user.FullName}، تم إنشاء حسابك بنجاح.";

        return region == null
            ? RedirectToAction("Index", "Region", new { returnUrl = model.ReturnUrl })
            : LocalRedirect(SafeReturnUrl(model.ReturnUrl));
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet, Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = await userManager.GetRolesAsync(user),
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!ModelState.IsValid)
        {
            model.Email = user.Email;
            model.Roles = await userManager.GetRolesAsync(user);
            model.CreatedAt = user.CreatedAt;
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "تم تحديث بياناتك.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet, Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task RestoreDefaultRegionAsync(string userId)
    {
        var ua = await db.UserAreas.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IsDefault);
        if (ua != null) CurrentRegion.FromUserArea(ua).WriteCookie(Response);
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
}
