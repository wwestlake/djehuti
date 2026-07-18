namespace Djehuti.Teacher.Services;

/// <summary>
/// Helper for generating S3 CDN URLs for static assets.
/// All assets are served from CloudFront CDN instead of the server to save disk space.
/// </summary>
public static class AssetHelper
{
    // CloudFront distribution endpoint (replace with actual CDN domain once configured)
    // Currently uses direct S3 access; will be replaced with CDN when distribution is live
    private const string S3_BUCKET = "us-east-1-886110331954-us-east-2-an";
    private const string S3_REGION = "us-east-2";
    private const string S3_BASE = $"https://{S3_BUCKET}.s3.{S3_REGION}.amazonaws.com";

    // Once CloudFront distribution is set up, use this instead:
    // private const string CDN_BASE = "https://cdn.lagdaemon.com";

    /// <summary>
    /// Get the full S3 URL for an asset.
    /// Example: GetAssetUrl("assets/learn/images/staff.png")
    /// Returns: https://bucket.s3.region.amazonaws.com/assets/learn/images/staff.png
    /// </summary>
    public static string GetAssetUrl(string path)
    {
        // Remove leading slash if present
        var cleanPath = path.TrimStart('/');
        return $"{S3_BASE}/{cleanPath}";
    }

    /// <summary>
    /// Get URL for Learn app graphics
    /// </summary>
    public static string GetLearnImageUrl(string filename) =>
        GetAssetUrl($"assets/learn/images/{filename}");

    /// <summary>
    /// Get URL for Learn app icons
    /// </summary>
    public static string GetLearnIconUrl(string filename) =>
        GetAssetUrl($"assets/learn/icons/{filename}");

    /// <summary>
    /// Get URL for Dashboard graphics
    /// </summary>
    public static string GetDashboardImageUrl(string filename) =>
        GetAssetUrl($"assets/dashboard/images/{filename}");

    /// <summary>
    /// Get URL for common/shared graphics
    /// </summary>
    public static string GetCommonImageUrl(string filename) =>
        GetAssetUrl($"assets/common/images/{filename}");

    /// <summary>
    /// Get URL for music notation PDFs
    /// </summary>
    public static string GetMusicPdfUrl(string filename) =>
        GetAssetUrl($"assets/learn/pdfs/{filename}");

    /// <summary>
    /// Get URL for archived/old files
    /// </summary>
    public static string GetArchiveUrl(string path) =>
        GetAssetUrl($"archives/{path}");

    /// <summary>
    /// Get URL for backup files
    /// </summary>
    public static string GetBackupUrl(string path) =>
        GetAssetUrl($"backups/{path}");
}
