namespace RuckR.Client.Services;

/// <summary>
/// Browser-processed profile avatar payload returned by the image resize module.
/// </summary>
public sealed class ProcessedProfileAvatar
{
    /// <summary>Processed image data URL.</summary>
    public string DataUrl { get; set; } = string.Empty;

    /// <summary>Processed image content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Processed image file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Original selected file size in bytes.</summary>
    public long OriginalSize { get; set; }

    /// <summary>Processed output size in bytes.</summary>
    public long OutputSize { get; set; }

    /// <summary>Original image width in pixels.</summary>
    public int OriginalWidth { get; set; }

    /// <summary>Original image height in pixels.</summary>
    public int OriginalHeight { get; set; }

    /// <summary>Processed image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Processed image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Whether the output differs from the selected source file.</summary>
    public bool WasResized { get; set; }
}
