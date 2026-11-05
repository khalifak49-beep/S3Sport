using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

public class NewsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(NewsCategory? category, string? search, int page = 1)
    {
        var query = db.News.AsNoTracking().Where(n => n.IsPublished);
        if (category.HasValue) query = query.Where(n => n.Category == category);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(n => n.TitleAr.Contains(search) || (n.Summary != null && n.Summary.Contains(search)));
        return View(new NewsListViewModel
        {
            Results = await PagedResult<NewsListItem>.CreateAsync(query.OrderByDescending(n => n.PublishedAt).ToListItems(), page, 12),
            Category = category, Search = search
        });
    }

    [HttpGet("/news/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var model = await db.News.AsNoTracking().Where(n => n.Slug == slug && n.IsPublished).Select(n => new NewsDetailsViewModel
        {
            Id = n.Id, Title = n.TitleAr, Summary = n.Summary, Body = n.Body, CoverImagePath = n.CoverImagePath,
            Category = n.Category, PublishedAt = n.PublishedAt, VideoUrl = n.VideoUrl, ViewCount = n.ViewCount,
            AuthorName = n.AuthorUser != null ? n.AuthorUser.FullName : null,
            TeamName = n.Team != null ? n.Team.NameAr : null, TeamSlug = n.Team != null ? n.Team.Slug : null,
            PlayerName = n.Player != null ? n.Player.FullNameAr : null, PlayerId = n.PlayerId,
            CompetitionName = n.Competition != null ? n.Competition.NameAr : null, CompetitionSlug = n.Competition != null ? n.Competition.Slug : null,
            MatchTitle = n.Match != null ? n.Match.HomeTeam.NameAr + " × " + n.Match.AwayTeam.NameAr : null, MatchId = n.MatchId,
            Media = n.MediaFiles.OrderBy(m => m.Id).Select(m => new MediaItem { Id = m.Id, Path = m.FilePath, Caption = m.Caption, Type = m.MediaType }).ToList()
        }).FirstOrDefaultAsync();
        if (model == null) return NotFound();

        model.VideoEmbedUrl = NewsHelper.YouTubeEmbedUrl(model.VideoUrl);
        await db.News.Where(n => n.Id == model.Id).ExecuteUpdateAsync(s => s.SetProperty(n => n.ViewCount, n => n.ViewCount + 1));

        var teamSlug = model.TeamSlug;
        var competitionSlug = model.CompetitionSlug;
        model.Related = await db.News.AsNoTracking()
            .Where(n => n.IsPublished && n.Id != model.Id && ((teamSlug != null && n.Team!.Slug == teamSlug) || (competitionSlug != null && n.Competition!.Slug == competitionSlug) || n.Category == model.Category))
            .OrderByDescending(n => n.PublishedAt).Take(4).ToListItems().ToListAsync();
        return View(model);
    }
}
