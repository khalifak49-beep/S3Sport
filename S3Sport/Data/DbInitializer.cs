using S3Sport.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace S3Sport.Data;

/// <summary>
/// Startup initialisation: applies migrations (when Database:AutoMigrate = true), ensures roles,
/// creates the first SuperAdmin from configuration, and optionally inserts demo areas/season.
/// Every step is idempotent so it is safe on every start.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));
        var db = sp.GetRequiredService<ApplicationDbContext>();

        if (config.GetValue<bool>("Database:AutoMigrate"))
        {
            logger.LogInformation("Applying pending migrations...");
            await db.Database.MigrateAsync();
        }

        await EnsureRolesAsync(sp.GetRequiredService<RoleManager<IdentityRole>>());
        await EnsureSuperAdminAsync(sp.GetRequiredService<UserManager<ApplicationUser>>(), config, logger);

        if (config.GetValue<bool>("Seed:DemoData"))
            await SeedDemoDataAsync(db, logger);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task EnsureSuperAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin:Email/Password not configured; no SuperAdmin account was created.");
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = config["SeedAdmin:FullName"] ?? "مدير النظام",
                IsActive = true
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not create SuperAdmin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("SuperAdmin account {Email} created.", email);
        }

        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.User })
        {
            if (!await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);
        }
    }

    /// <summary>Sample areas, villages and the current season so the platform is usable right after setup.</summary>
    private static async Task SeedDemoDataAsync(ApplicationDbContext db, ILogger logger)
    {
        var demoAreas = new Dictionary<string, (string Ar, string En)[]>
        {
            ["As Seeb"] = [("الخوض", "Al Khoudh"), ("المعبيلة", "Al Mabaila"), ("الموالح", "Al Mawaleh"), ("الحيل", "Al Hail")],
            ["Bawshar"] = [("العذيبة", "Al Azaiba"), ("الأنصب", "Al Ansab")],
            ["Nizwa"] = [("فرق", "Firq"), ("تنوف", "Tanuf"), ("بركة الموز", "Birkat Al Mouz")],
            ["Sohar"] = [("فلج القبائل", "Falaj Al Qabail"), ("الطريف", "Al Tareef")],
            ["Barka"] = [("الصومحان", "As Sawmahan"), ("حي عاصم", "Hay Asim")],
            ["Salalah"] = [("صلالة الجديدة", "New Salalah"), ("عوقد", "Awqad")]
        };

        var wilayats = await db.Wilayats.Where(w => demoAreas.Keys.Contains(w.NameEn)).ToListAsync();
        var existingAreas = await db.Areas.Select(a => new { a.WilayatId, a.NameAr }).ToListAsync();
        var added = 0;
        foreach (var wilayat in wilayats)
        {
            foreach (var (ar, en) in demoAreas[wilayat.NameEn])
            {
                if (existingAreas.Any(a => a.WilayatId == wilayat.Id && a.NameAr == ar)) continue;
                db.Areas.Add(new Area { WilayatId = wilayat.Id, NameAr = ar, NameEn = en });
                added++;
            }
        }
        await db.SaveChangesAsync();

        var khoudh = await db.Areas.FirstOrDefaultAsync(a => a.NameEn == "Al Khoudh");
        if (khoudh != null && !await db.Villages.AnyAsync(v => v.AreaId == khoudh.Id))
        {
            db.Villages.AddRange(
                new Village { AreaId = khoudh.Id, NameAr = "الخوض القديمة", NameEn = "Old Al Khoudh" },
                new Village { AreaId = khoudh.Id, NameAr = "الخوض السادسة", NameEn = "Al Khoudh 6" });
        }

        if (!await db.Seasons.AnyAsync())
        {
            db.Seasons.Add(new Season
            {
                Name = "2026-2027",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2027, 6, 30),
                IsCurrent = true
            });
        }

        await db.SaveChangesAsync();
        if (added > 0) logger.LogInformation("Demo data: {Count} areas added.", added);
    }
}
