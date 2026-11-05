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
    IWebHostEnvironment env,
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

    /// <summary>
    /// Sign-up starts with a choice: a regular user (follows teams and matches) or a team (account + team application).
    /// <paramref name="type"/> = "user" shows the user form directly; "team" goes to the team sign-up.
    /// </summary>
    [HttpGet, AllowAnonymous]
    public IActionResult Register(string? type = null, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (type == "team") return RedirectToAction(nameof(RegisterTeam));
        if (type != "user") return View("RegisterChoice", returnUrl);
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> RegisterTeam()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Create", "TeamApplications");
        var model = new RegisterTeamViewModel();
        var region = CurrentRegion.FromRequest(Request);
        if (region != null)
        {
            model.GovernorateId = region.GovernorateId;
            model.WilayatId = region.WilayatId ?? 0;
            model.AreaId = region.AreaId;
            model.VillageId = region.VillageId;
        }
        await TeamFormHelper.FillListsAsync(db, model);
        return View(model);
    }

    /// <summary>
    /// Creates the manager's account and submits the team application in one transaction: either both exist
    /// afterwards or neither does. The team itself is created when the platform approves the application.
    /// </summary>
    [HttpPost, AllowAnonymous]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> RegisterTeam(RegisterTeamViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Create", "TeamApplications");

        await TeamFormHelper.ValidateApplicationAsync(db, model, ModelState, applicantUserId: null, logoRequired: true);
        var email = model.Email.Trim();
        if (!string.IsNullOrEmpty(email) && await userManager.FindByEmailAsync(email) != null)
            ModelState.AddModelError(nameof(model.Email), "هذا البريد مسجل مسبقاً. سجّل الدخول ثم قدّم طلب الفريق من حسابك.");
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.ContactPhone) ? null : model.ContactPhone.Trim(), IsActive = true
        };
        // Password rules are checked up front so every error shows at once and no logo is stored for nothing.
        if (!string.IsNullOrEmpty(model.Password))
            foreach (var validator in userManager.PasswordValidators)
                foreach (var error in (await validator.ValidateAsync(userManager, user, model.Password)).Errors)
                    ModelState.AddModelError(nameof(model.Password), error.Description);
        if (!ModelState.IsValid)
        {
            await TeamFormHelper.FillListsAsync(db, model);
            return View(model);
        }

        var logoPath = await FileUploadHelper.SaveImageAsync(model.Logo!, env, "teams");
        TeamApplication? application = null;
        try
        {
            await db.InTransactionAsync(async () =>
            {
                var created = await userManager.CreateAsync(user, model.Password);
                if (!created.Succeeded) throw new SignUpRejectedException(created);
                var role = await userManager.AddToRoleAsync(user, AppRoles.User);
                if (!role.Succeeded) throw new SignUpRejectedException(role);

                application = new TeamApplication
                {
                    ApplicantUserId = user.Id, SportId = SeedData.FootballSportId, Status = TeamStatus.Pending,
                    SubmittedAt = DateTime.UtcNow, LogoPath = logoPath
                };
                TeamFormHelper.MapApplication(model, application);
                db.TeamApplications.Add(application);
                // The team's region becomes the new account's default region.
                if (model.AreaId != null)
                    db.UserAreas.Add(new UserArea { UserId = user.Id, GovernorateId = model.GovernorateId, WilayatId = model.WilayatId, AreaId = model.AreaId, IsDefault = true });
                await db.SaveChangesAsync();
                db.AddAudit(HttpContext, "تسجيل حساب فريق وتقديم طلب", nameof(TeamApplication), application.Id, application.NameAr);
                await db.SaveChangesAsync();
            });
        }
        catch (SignUpRejectedException ex)
        {
            FileUploadHelper.Delete(env, logoPath);
            db.ChangeTracker.Clear();
            foreach (var error in ex.Result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await TeamFormHelper.FillListsAsync(db, model);
            return View(model);
        }
        catch
        {
            FileUploadHelper.Delete(env, logoPath);
            throw;
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        logger.LogInformation("New team account registered: {Email} ({Team})", user.Email, application!.NameAr);
        TempData["Success"] = $"مرحباً {user.FullName}، تم إنشاء حسابك وإرسال طلب تسجيل «{application.NameAr}» للمراجعة. يصلك إشعار عند الموافقة.";
        return RedirectToAction("Details", "TeamApplications", new { id = application.Id });
    }

    /// <summary>Identity refused the account inside the sign-up transaction: roll everything back.</summary>
    private sealed class SignUpRejectedException(IdentityResult result) : Exception("Sign-up rejected")
    {
        public IdentityResult Result { get; } = result;
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
