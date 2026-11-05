namespace S3Sport.Infrastructure;

/// <summary>
/// Validates and stores uploaded images under wwwroot/uploads/{folder}.
/// Files are checked by extension, size AND magic bytes, then saved under a random name,
/// so the client's file name or content type is never trusted.
/// </summary>
public static class FileUploadHelper
{
    public const long MaxImageBytes = 2 * 1024 * 1024;
    public const string AllowedImageExtensions = ".jpg,.jpeg,.png,.webp";

    private static readonly Dictionary<string, byte[]> Signatures = new()
    {
        [".jpg"] = [0xFF, 0xD8, 0xFF],
        [".jpeg"] = [0xFF, 0xD8, 0xFF],
        [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        [".webp"] = [0x52, 0x49, 0x46, 0x46] // "RIFF" + 4 bytes + "WEBP"
    };

    /// <summary>Returns an Arabic error message, or null when the file is acceptable.</summary>
    public static string? ValidateImage(IFormFile? file)
    {
        if (file == null || file.Length == 0) return "يرجى اختيار ملف صورة.";
        if (file.Length > MaxImageBytes) return "حجم الصورة يجب ألا يتجاوز 2 ميجابايت.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Signatures.TryGetValue(ext, out var signature)) return "نوع الملف غير مسموح. الأنواع المسموحة: JPG, PNG, WEBP.";

        using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = stream.Read(header, 0, header.Length);
        if (read < signature.Length || !header.AsSpan(0, signature.Length).SequenceEqual(signature))
            return "محتوى الملف لا يطابق صورة صالحة.";
        if (ext == ".webp" && (read < 12 || header[8] != 'W' || header[9] != 'E' || header[10] != 'B' || header[11] != 'P'))
            return "محتوى الملف لا يطابق صورة صالحة.";

        return null;
    }

    /// <summary>Saves a validated image and returns its web path, e.g. "/uploads/teams/abc.png".</summary>
    public static async Task<string> SaveImageAsync(IFormFile file, IWebHostEnvironment env, string folder)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var directory = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using (var target = new FileStream(Path.Combine(directory, fileName), FileMode.CreateNew))
            await file.CopyToAsync(target);

        return $"/uploads/{folder}/{fileName}";
    }

    /// <summary>Deletes a previously uploaded file. Paths outside /uploads are ignored.</summary>
    public static void Delete(IWebHostEnvironment env, string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath) || !webPath.StartsWith("/uploads/", StringComparison.Ordinal)) return;

        var uploadsRoot = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        var fullPath = Path.GetFullPath(Path.Combine(env.WebRootPath, webPath.TrimStart('/')));
        if (fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
