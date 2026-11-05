using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.WebEncoders;
using S3Sport.Data;
using S3Sport.Hubs;
using S3Sport.Infrastructure;
using S3Sport.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging (Serilog: console + rolling daily files, configured in appsettings "Serilog") ----------
builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---------- Database ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 3);
        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
    // Soft-deleted teams/players intentionally hide their dependent rows.
    options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

// ---------- Data protection ----------
// Auth cookies and antiforgery tokens are encrypted with these keys. Persisting them means an IIS
// app-pool recycle or a redeploy does not sign everyone out or break forms that are open.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath)) keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("S3Sport");

// ---------- Identity ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<ArabicIdentityErrorDescriber>();

// Role changes and deactivation update the security stamp; re-check it every minute
// so those changes reach signed-in users quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
// Roles granted by someone else (team approval, admin role edits) reach the user on their next request.
builder.Services.AddSingleton<RoleRefresh>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "S3Sport.Auth";
    options.Cookie.HttpOnly = true;
    // HTTPS-only cookies in production; plain HTTP is allowed only for local development.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
});
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "S3Sport.Antiforgery";
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// ---------- Authorization policies ----------
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.SuperAdminOnly, p => p.RequireRole(AppRoles.SuperAdmin))
    .AddPolicy(AppPolicies.AdminAccess, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin))
    .AddPolicy(AppPolicies.ManageTeams, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.TeamManager))
    .AddPolicy(AppPolicies.ManageCompetitions, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.TournamentManager))
    .AddPolicy(AppPolicies.OperateMatches, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.TournamentManager, AppRoles.MatchOperator, AppRoles.TeamManager))
    .AddPolicy(AppPolicies.ManageContent, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.ContentManager));

// ---------- MVC ----------
builder.Services.AddControllersWithViews(options =>
{
    // CSRF protection on every unsafe (POST/PUT/DELETE) MVC action.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustNotBeNullAccessor(_ => "هذا الحقل مطلوب.");
    messages.SetAttemptedValueIsInvalidAccessor((value, _) => $"القيمة «{value}» غير صالحة.");
    messages.SetValueIsInvalidAccessor(value => $"القيمة «{value}» غير صالحة.");
    messages.SetValueMustBeANumberAccessor(_ => "يجب إدخال رقم صحيح.");
    messages.SetMissingBindRequiredValueAccessor(_ => "هذا الحقل مطلوب.");
    messages.SetUnknownValueIsInvalidAccessor(_ => "القيمة غير صالحة.");
    messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"القيمة «{value}» غير صالحة.");
    messages.SetNonPropertyValueMustBeANumberAccessor(() => "يجب إدخال رقم صحيح.");
    messages.SetMissingRequestBodyRequiredValueAccessor(() => "لم يتم إرسال بيانات.");
}).AddJsonOptions(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

// Emit Arabic text as-is instead of &#x...; entities (HTML-sensitive characters are still escaped).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Largest accepted upload (news gallery). Individual actions set lower limits; IIS has its own limit in web.config.
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = 32 * 1024 * 1024);

// ---------- SignalR (live scores, notifications) ----------
builder.Services.AddSignalR(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());

// ---------- Web Push (phone / desktop notifications while the site is closed) ----------
builder.Services.AddSingleton<PushKeys>();
builder.Services.AddHttpClient(nameof(PushWorker), c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHostedService<PushWorker>();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var arabic = new CultureInfo("ar-OM");
    // Western digits and Gregorian calendar keep dates/numbers consistent across browsers.
    arabic.DateTimeFormat.Calendar = new GregorianCalendar();
    arabic.NumberFormat.DigitSubstitution = DigitShapes.None;
    // ICU's ar-OM uses "؜-" (Arabic letter mark + minus), "٫" and "٬": HTML forms send "-2" and "23.58",
    // which would then fail model binding (silently becoming 0). Use the ASCII symbols browsers send.
    var nf = arabic.NumberFormat;
    nf.NegativeSign = "-";
    nf.PositiveSign = "+";
    nf.NumberDecimalSeparator = nf.CurrencyDecimalSeparator = nf.PercentDecimalSeparator = ".";
    nf.NumberGroupSeparator = nf.CurrencyGroupSeparator = nf.PercentGroupSeparator = ",";
    options.DefaultRequestCulture = new RequestCulture(arabic);
    options.SupportedCultures = [arabic];
    options.SupportedUICultures = [arabic];
});

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

// ---------- Pipeline ----------
// Behind a reverse proxy or tunnel on this machine (e.g. Cloudflare Tunnel), the visitor's scheme and IP arrive in
// X-Forwarded-Proto / X-Forwarded-For. Only loopback proxies are trusted (the framework default), so a client on
// the internet cannot spoof them. Without this, HTTPS visitors look like plain HTTP: secure cookies and Web Push fail.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Defence-in-depth headers. Inline styles/scripts are used by the views (theme bootstrap, charts),
// so CSP allows 'unsafe-inline' but blocks every foreign origin except Google Fonts and YouTube embeds.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "SAMEORIGIN";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    h.ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' ws: wss:; " +
        "frame-src https://www.youtube-nocookie.com; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
    await next();
});

app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // The service worker must always be revalidated so updates reach installed apps.
        if (ctx.Context.Request.Path.Equals("/sw.js"))
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.UseSerilogRequestLogging(o => o.GetLevel = (http, _, ex) =>
    ex != null || http.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
    : http.Request.Path.StartsWithSegments("/hubs") || http.Request.Path.StartsWithSegments("/lib") ? Serilog.Events.LogEventLevel.Verbose
    : Serilog.Events.LogEventLevel.Information);
app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseRoleRefresh();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHub<LiveHub>("/hubs/live");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

try
{
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "S3Sport stopped unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
