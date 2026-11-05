using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>
/// Read-only overview of the fixed roles and what each one is allowed to do.
/// Roles are assigned per user from the Users page.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class RolesController(ApplicationDbContext db) : Controller
{
    private static readonly Dictionary<string, string[]> Permissions = new()
    {
        [AppRoles.SuperAdmin] = ["كل صلاحيات المنصة", "منح وسحب صلاحيات المدراء", "إدارة إعدادات النظام"],
        [AppRoles.Admin] = ["لوحة الإدارة", "مراجعة طلبات الفرق واعتمادها", "إدارة المناطق والفرق واللاعبين", "إدارة البطولات والمباريات والأخبار", "إدارة المستخدمين (عدا المدراء)", "سجل العمليات"],
        [AppRoles.TeamManager] = ["إدارة فريقه فقط (البيانات، اللاعبين، الصور)", "لا يستطيع إدارة فريق آخر"],
        [AppRoles.TournamentManager] = ["إدارة البطولات المسندة إليه فقط", "جدولة مبارياتها وإدارة الترتيب", "تسجيل أحداث مبارياتها"],
        [AppRoles.MatchOperator] = ["تسجيل أحداث المباريات المسندة إليه فقط", "لا يستطيع تعديل مباراة غير مسندة إليه"],
        [AppRoles.ContentManager] = ["نشر وتعديل الأخبار", "رفع الصور والفيديوهات"],
        [AppRoles.User] = ["تصفح المنصة واختيار المنطقة", "متابعة الفرق واللاعبين والبطولات", "تقديم طلب تسجيل فريق"]
    };

    public async Task<IActionResult> Index()
    {
        var counts = await db.UserRoles.Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .GroupBy(n => n).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var model = AppRoles.All.Select(r => new RoleSummary
        {
            Name = r,
            DisplayName = AppRoles.DisplayNames[r],
            UserCount = counts.GetValueOrDefault(r),
            Permissions = Permissions[r]
        }).ToList();
        return View(model);
    }
}
