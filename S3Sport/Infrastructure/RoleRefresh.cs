using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

/// <summary>
/// Roles live in the sign-in cookie, so a role granted by someone else (e.g. an admin approving a team) would only
/// reach the user at the next security-stamp check. Code that changes a user's roles calls <see cref="MarkChanged"/>;
/// the middleware below re-issues that user's cookie on their very next request, without signing them out.
/// In-memory: designed for a single app instance (the one-minute security-stamp check still covers anything missed).
/// </summary>
public class RoleRefresh
{
    private readonly ConcurrentDictionary<string, byte> pending = new();

    public void MarkChanged(string userId) => pending[userId] = 0;

    public bool TryConsume(string userId) => pending.TryRemove(userId, out _);
}

public static class RoleRefreshMiddleware
{
    /// <summary>Must run after authentication and before authorization.</summary>
    public static IApplicationBuilder UseRoleRefresh(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null && ctx.RequestServices.GetRequiredService<RoleRefresh>().TryConsume(userId))
        {
            var users = ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var signIn = ctx.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);
            if (user != null && user.IsActive)
            {
                await signIn.RefreshSignInAsync(user);
                ctx.User = await signIn.CreateUserPrincipalAsync(user); // this request already sees the new roles
            }
        }
        await next();
    });
}
