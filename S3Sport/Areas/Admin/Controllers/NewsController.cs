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

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>
/// News, photos and videos. A news item can be linked to a team, player, competition and/or match;
/// publishing it for the first time notifies the followers of those items.
/// The body is plain text (rendered with line breaks), so no HTML is ever stored or rendered from user input.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.ManageContent)]
public class NewsController(ApplicationDbContext db, IWebHostEnvironment env, IHubContext<LiveHub> hub) : Controller
{
    private const int MaxGalleryFiles = 12;

    public async Task<IActionResult> Index(NewsCategory? category, string? search, bool? published, int page = 1)
    {
        var query = db.News.AsNoTracking();
        if (category.HasValue) query = query.Where(n => n.Category == category);
        if (published.HasValue) query = query.Where(n => n.IsPublished == published);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(n => n.TitleAr.Contains(search));
        return View(new NewsListViewModel
        {
            Results = await PagedResult<NewsListItem>.CreateAsync(query.OrderByDescending(n => n.CreatedAt).ToListItems(), page, 20),
            Category = category, Search = search, Published = published
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? teamId, int? competitionId, int? matchId) =>
        View("Edit", await FillAsync(new NewsFormViewModel { TeamId = teamId, CompetitionId = competitionId, MatchId = matchId }));

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var n = await db.News.AsNoTracking().Include(x => x.MediaFiles).FirstOrDefaultAsync(x => x.Id == id);
        if (n == null) return NotFound();
        return View(await FillAsync(new NewsFormViewModel
        {
            Id = n.Id, TitleAr = n.TitleAr, Summary = n.Summary, Body = n.Body, Category = n.Category, VideoUrl = n.VideoUrl,
            TeamId = n.TeamId, PlayerId = n.PlayerId, CompetitionId = n.CompetitionId, MatchId = n.MatchId, IsPublished = n.IsPublished,
            CoverImagePath = n.CoverImagePath, RowVersion = Convert.ToBase64String(n.RowVersion),
            ExistingMedia = n.MediaFiles.OrderBy(m => m.Id).Select(m => new MediaItem { Id = m.Id, Path = m.FilePath, Caption = m.Caption, Type = m.MediaType }).ToList()
        }));
    }

    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    public async Task<IActionResult> Save(NewsFormViewModel model)
    {
        if (model.Cover is { Length: > 0 } && FileUploadHelper.ValidateImage(model.Cover) is { } coverErr) ModelState.AddModelError(nameof(model.Cover), coverErr);
        var gallery = model.Gallery.Where(f => f is { Length: > 0 }).ToList();
        if (gallery.Count > MaxGalleryFiles) ModelState.AddModelError(nameof(model.Gallery), $"الحد الأقصى {MaxGalleryFiles} صورة في المرة الواحدة.");
        foreach (var f in gallery)
            if (FileUploadHelper.ValidateImage(f) is { } err) { ModelState.AddModelError(nameof(model.Gallery), $"{f.FileName}: {err}"); break; }
        if (!string.IsNullOrWhiteSpace(model.VideoUrl) && NewsHelper.YouTubeEmbedUrl(model.VideoUrl) == null)
            ModelState.AddModelError(nameof(model.VideoUrl), "يُقبل رابط يوتيوب فقط.");
        if (model.TeamId != null && !await db.Teams.AnyAsync(t => t.Id == model.TeamId)) ModelState.AddModelError(nameof(model.TeamId), "الفريق غير صالح.");
        if (model.PlayerId != null && !await db.Players.AnyAsync(p => p.Id == model.PlayerId)) ModelState.AddModelError(nameof(model.PlayerId), "اللاعب غير صالح.");
        if (model.CompetitionId != null && !await db.Competitions.AnyAsync(c => c.Id == model.CompetitionId)) ModelState.AddModelError(nameof(model.CompetitionId), "البطولة غير صالحة.");
        if (model.MatchId != null && !await db.Matches.AnyAsync(m => m.Id == model.MatchId)) ModelState.AddModelError(nameof(model.MatchId), "المباراة غير صالحة.");
        if (!ModelState.IsValid)
        {
            if (model.Id != 0)
            {
                var existing = await db.News.AsNoTracking().Include(x => x.MediaFiles).FirstOrDefaultAsync(x => x.Id == model.Id);
                model.CoverImagePath = existing?.CoverImagePath;
                model.ExistingMedia = existing?.MediaFiles.Select(m => new MediaItem { Id = m.Id, Path = m.FilePath, Caption = m.Caption, Type = m.MediaType }).ToList() ?? [];
            }
            return View("Edit", await FillAsync(model));
        }

        News? news = model.Id == 0 ? null : await db.News.FirstOrDefaultAsync(n => n.Id == model.Id);
        if (model.Id != 0 && news == null) return NotFound();
        var isNew = news == null;
        var wasPublished = news?.IsPublished ?? false;
        if (news == null)
        {
            news = new News
            {
                AuthorUserId = User.GetUserId(),
                Slug = await SlugHelper.GenerateUniqueAsync($"news-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6]}",
                    s => db.News.IgnoreQueryFilters().AnyAsync(n => n.Slug == s), "news")
            };
            db.News.Add(news);
        }
        else db.SetOriginalRowVersion(news, model.RowVersion, n => n.RowVersion);

        news.TitleAr = model.TitleAr.Trim();
        news.Summary = model.Summary?.Trim();
        news.Body = model.Body.Trim();
        news.Category = model.Category;
        news.VideoUrl = string.IsNullOrWhiteSpace(model.VideoUrl) ? null : model.VideoUrl.Trim();
        news.TeamId = model.TeamId;
        news.PlayerId = model.PlayerId;
        news.CompetitionId = model.CompetitionId;
        news.MatchId = model.MatchId;
        news.IsPublished = model.IsPublished;
        if (model.IsPublished) news.PublishedAt ??= DateTime.UtcNow;

        var newFiles = new List<string>();
        if (model.Cover is { Length: > 0 })
        {
            var path = await FileUploadHelper.SaveImageAsync(model.Cover, env, "news");
            newFiles.Add(path);
            FileUploadHelper.Delete(env, news.CoverImagePath);
            news.CoverImagePath = path;
        }
        foreach (var f in gallery)
        {
            var path = await FileUploadHelper.SaveImageAsync(f, env, "news");
            newFiles.Add(path);
            news.MediaFiles.Add(new MediaFile
            {
                MediaType = MediaType.Image, FilePath = path, OriginalFileName = Path.GetFileName(f.FileName),
                ContentType = f.ContentType, SizeBytes = f.Length, UploadedByUserId = User.GetUserId(), TeamId = model.TeamId, PlayerId = model.PlayerId
            });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            foreach (var f in newFiles) FileUploadHelper.Delete(env, f);
            TempData["Error"] = "تم تعديل الخبر من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        db.AddAudit(HttpContext, isNew ? "إضافة خبر" : "تعديل خبر", nameof(News), news.Id, news.TitleAr);
        await db.SaveChangesAsync();

        // First publication → followers of the linked team / player / competition.
        if (news.IsPublished && !wasPublished)
        {
            var teamIds = new List<int>();
            if (news.TeamId is { } t) teamIds.Add(t);
            if (news.MatchId is { } mid)
                teamIds.AddRange(await db.Matches.Where(m => m.Id == mid).SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).ToListAsync());
            var sent = await NotificationHelper.NotifyFollowersAsync(db, hub, teamIds.Distinct().ToList(),
                news.PlayerId is { } p ? [p] : [], news.CompetitionId, NotificationType.NewsPublished,
                $"خبر جديد: {news.TitleAr}", news.Summary, $"/news/{news.Slug}", newsId: news.Id);
            TempData["Success"] = sent > 0 ? $"تم نشر الخبر وإشعار {sent} متابع." : "تم نشر الخبر.";
        }
        else TempData["Success"] = "تم حفظ الخبر.";

        return RedirectToAction(nameof(Edit), new { id = news.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMedia(int id, int mediaId)
    {
        var media = await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == mediaId && m.NewsId == id);
        if (media == null) return NotFound();
        db.MediaFiles.Remove(media); // soft delete; the file is removed from disk
        FileUploadHelper.Delete(env, media.FilePath);
        db.AddAudit(HttpContext, "حذف صورة من خبر", nameof(News), id, media.FilePath);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف الصورة.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var news = await db.News.FindAsync(id);
        if (news == null) return NotFound();
        db.News.Remove(news); // soft delete
        db.AddAudit(HttpContext, "حذف خبر", nameof(News), id, news.TitleAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف الخبر.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<NewsFormViewModel> FillAsync(NewsFormViewModel model)
    {
        model.Teams = await db.Teams.AsNoTracking().Where(t => t.Status == TeamStatus.Approved).OrderBy(t => t.NameAr)
            .Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync();
        model.Players = await db.Players.AsNoTracking().OrderBy(p => p.FullNameAr).Take(500)
            .Select(p => new SelectListItem(p.FullNameAr, p.Id.ToString())).ToListAsync();
        model.Competitions = await db.Competitions.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new SelectListItem(c.NameAr, c.Id.ToString())).ToListAsync();
        var since = DateTime.UtcNow.AddDays(-60);
        model.Matches = await db.Matches.AsNoTracking().Where(m => m.KickoffAt >= since || m.Id == model.MatchId).OrderByDescending(m => m.KickoffAt).Take(200)
            .Select(m => new SelectListItem(m.HomeTeam.NameAr + " × " + m.AwayTeam.NameAr, m.Id.ToString())).ToListAsync();
        return model;
    }
}
