using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

public static class DisplayExtensions
{
    /// <summary>Oman is UTC+4 all year (no daylight saving), so a fixed offset is exact.</summary>
    public static readonly TimeSpan MuscatOffset = TimeSpan.FromHours(4);

    public static DateTime ToMuscat(this DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(MuscatOffset);

    /// <summary>A wall-clock time entered in Muscat → UTC for storage.</summary>
    public static DateTime FromMuscat(this DateTime muscatLocal) => DateTime.SpecifyKind(muscatLocal - MuscatOffset, DateTimeKind.Utc);

    public static string ToMuscatString(this DateTime utc, string format = "yyyy/MM/dd HH:mm") =>
        utc.ToMuscat().ToString(format);

    public static string ToMuscatString(this DateTime? utc, string format = "yyyy/MM/dd HH:mm") =>
        utc.HasValue ? utc.Value.ToMuscatString(format) : "—";

    public static string GetDisplayName(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()
            ?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();

    public static string BadgeClass(this TeamStatus status) => status switch
    {
        TeamStatus.Pending => "text-bg-warning",
        TeamStatus.Approved => "text-bg-success",
        TeamStatus.Rejected => "text-bg-danger",
        TeamStatus.Suspended => "text-bg-secondary",
        TeamStatus.Guest => "text-bg-info",
        _ => "text-bg-light"
    };
}

public static partial class SlugHelper
{
    /// <summary>Lower-case latin slug from an English name ("Al Khoudh SC" → "al-khoudh-sc").</summary>
    public static string Generate(string? text, string fallback = "item")
    {
        var normalized = (text ?? string.Empty).Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        var slug = NonAlphanumeric().Replace(normalized, "-").Trim('-');
        if (slug.Length > 150) slug = slug[..150].Trim('-');
        return slug.Length == 0 ? fallback : slug;
    }

    /// <summary>Appends -2, -3, ... until <paramref name="exists"/> returns false.</summary>
    public static async Task<string> GenerateUniqueAsync(string? text, Func<string, Task<bool>> exists, string fallback = "item")
    {
        var baseSlug = Generate(text, fallback);
        var slug = baseSlug;
        for (var i = 2; await exists(slug); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}

public static class HtmlHelperExtensions
{
    /// <summary>
    /// Team/entity logo, or a coloured initial badge when no logo was uploaded.
    /// Team and competition crests are shown whole (shields, badges); player photos are cropped to a circle.
    /// </summary>
    public static Microsoft.AspNetCore.Html.IHtmlContent EntityLogo(this Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper html,
        string? path, string name, int size = 48)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(name);
        if (!string.IsNullOrEmpty(path))
        {
            var crest = path.StartsWith("/uploads/teams/", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/uploads/competitions/", StringComparison.OrdinalIgnoreCase);
            return new Microsoft.AspNetCore.Html.HtmlString(
                $"<img src=\"{System.Net.WebUtility.HtmlEncode(path)}\" alt=\"{encodedName}\" class=\"entity-logo{(crest ? " entity-logo-crest" : "")}\" style=\"width:{size}px;height:{size}px\" loading=\"lazy\" />");
        }

        var initial = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1]);
        // Stable across restarts (string.GetHashCode is randomised per process).
        var hue = name.Sum(c => (int)c) * 37 % 360;
        return new Microsoft.AspNetCore.Html.HtmlString(
            $"<span class=\"entity-logo entity-logo-placeholder\" style=\"width:{size}px;height:{size}px;font-size:{size / 2.4:0}px;--hue:{hue}\" aria-label=\"{encodedName}\">{initial}</span>");
    }
}
