using System.Text.RegularExpressions;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

public static partial class NewsHelper
{
    /// <summary>
    /// Privacy-friendly embed URL for a YouTube link (watch, youtu.be, shorts, embed), or null for anything else.
    /// Only the 11-character video id is reused, so arbitrary URLs never reach an iframe.
    /// </summary>
    public static string? YouTubeEmbedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = YouTubeId().Match(url);
        return m.Success ? $"https://www.youtube-nocookie.com/embed/{m.Groups["id"].Value}" : null;
    }

    public static IQueryable<NewsListItem> ToListItems(this IQueryable<News> news) =>
        news.Select(n => new NewsListItem
        {
            Id = n.Id, Slug = n.Slug, Title = n.TitleAr, Summary = n.Summary, CoverImagePath = n.CoverImagePath,
            Category = n.Category, PublishedAt = n.PublishedAt, IsPublished = n.IsPublished, HasVideo = n.VideoUrl != null,
            TeamName = n.Team != null ? n.Team.NameAr : null, CompetitionName = n.Competition != null ? n.Competition.NameAr : null,
            ViewCount = n.ViewCount
        });

    [GeneratedRegex(@"^https?://(?:www\.|m\.)?(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|embed/)|youtu\.be/)(?<id>[A-Za-z0-9_-]{11})(?:[?&#].*)?$")]
    private static partial Regex YouTubeId();
}
