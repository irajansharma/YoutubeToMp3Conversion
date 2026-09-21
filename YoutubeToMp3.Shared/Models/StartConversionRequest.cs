namespace YoutubeToMp3.Shared.Models;

public class StartConversionRequest
{
    public string Url { get; set; } = string.Empty;

    /// <summary>MP3 bitrate in kbps. Typical values: 128, 192, 256, 320.</summary>
    public int BitrateKbps { get; set; } = 192;

    /// <summary>
    /// If set, splits the output into chunks of this many minutes and returns
    /// a .zip containing them. Useful for very long (e.g. 10-hour) videos.
    /// </summary>
    public int? SplitMinutes { get; set; }

    /// <summary>Optional custom output file name (without extension).</summary>
    public string? FileName { get; set; }
}
