using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeToMp3.Api.Services;

public class YoutubeDownloader
{
    private readonly YoutubeClient _youtube;
    private const int MaxRetries = 4;

    public YoutubeDownloader()
    {
        // Long timeout: audio for a 10-hour video can take a while to pull,
        // and the default 100s HttpClient timeout would kill it mid-download.
        var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(4) };
        _youtube = new YoutubeClient(httpClient);
    }

    public async Task<Video> GetVideoInfoAsync(string url, CancellationToken ct)
    {
        return await _youtube.Videos.GetAsync(url, ct);
    }

    /// <summary>
    /// Downloads the highest-bitrate audio-only stream to a temp file, retrying
    /// on transient network failures (common on very large downloads).
    /// </summary>
    public async Task<string> DownloadAudioAsync(
        string url, string tempDirectory, IProgress<double> progress, CancellationToken ct)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(url, ct);

        // Sorting manually instead of relying on a specific
        // GetWithHighestBitrate() extension overload — that helper's exact
        // signature has shifted across YoutubeExplode versions, whereas
        // plain LINQ over IStreamInfo/AudioOnlyStreamInfo is stable.
        var audioStreamInfo = manifest.Streams
            .OfType<AudioOnlyStreamInfo>()
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No audio-only stream was found for this video.");

        var tempFilePath = Path.Combine(tempDirectory, $"{Guid.NewGuid()}.{audioStreamInfo.Container.Name}");
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempFilePath, progress, ct);
                return tempFilePath;
            }
            catch (Exception ex) when (attempt < MaxRetries && ex is not OperationCanceledException)
            {
                lastException = ex;
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
            }
        }

        throw new InvalidOperationException($"Failed to download audio after {MaxRetries} attempts.", lastException);
    }
}
