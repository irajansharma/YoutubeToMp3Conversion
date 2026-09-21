using System.IO.Compression;
using YoutubeToMp3.Shared.Models;

namespace YoutubeToMp3.Api.Services;

/// <summary>
/// Consumes queued job IDs and runs download + conversion for each one,
/// with limited concurrency so a couple of long 10-hour jobs don't starve
/// the machine's CPU/bandwidth.
/// </summary>
public class ConversionBackgroundService : BackgroundService
{
    private readonly ConversionQueue _queue;
    private readonly IJobStore _jobStore;
    private readonly YoutubeDownloader _downloader = new();
    private readonly AudioConverter _converter = new();
    private readonly SemaphoreSlim _concurrencyLimiter = new(initialCount: 2);
    private readonly string _tempDir;
    private readonly string _outputDir;
    private readonly ILogger<ConversionBackgroundService> _logger;

    public ConversionBackgroundService(
        ConversionQueue queue, IJobStore jobStore, IConfiguration config,
        ILogger<ConversionBackgroundService> logger)
    {
        _queue = queue;
        _jobStore = jobStore;
        _logger = logger;

        var storageRoot = config["StorageRoot"] ?? Path.Combine(AppContext.BaseDirectory, "Storage");
        _tempDir = Path.Combine(storageRoot, "temp");
        _outputDir = Path.Combine(storageRoot, "output");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_outputDir);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            // Fire and forget so the loop keeps pulling more jobs up to the
            // concurrency limit; each task releases the slot when it's done.
            _ = ProcessJobAsync(jobId, stoppingToken)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.LogError(t.Exception, "Unhandled error processing job {JobId}", jobId);
                    _concurrencyLimiter.Release();
                }, TaskScheduler.Default);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken hostToken)
    {
        if (!_jobStore.TryGet(jobId, out var job)) return;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(hostToken, job.Cts.Token);
        var ct = linkedCts.Token;

        string? tempAudioPath = null;
        string? segmentDir = null;

        try
        {
            job.Status = JobStatus.FetchingInfo;
            job.Message = "Fetching video info...";

            var video = await _downloader.GetVideoInfoAsync(job.Options.Url, ct);
            job.Title = video.Title;
            job.Duration = video.Duration;

            job.Status = JobStatus.Downloading;
            job.Message = "Downloading audio stream...";
            var downloadProgress = new Progress<double>(p => job.ProgressPercent = p * 50);

            tempAudioPath = await _downloader.DownloadAudioAsync(job.Options.Url, _tempDir, downloadProgress, ct);

            job.Status = JobStatus.Converting;
            job.Message = "Converting to MP3...";

            var safeTitle = SanitizeFileName(job.Options.FileName ?? video.Title ?? "audio");
            var totalSeconds = job.Duration?.TotalSeconds ?? 0;

            var conversionProgress = new Progress<TimeSpan>(elapsed =>
            {
                if (totalSeconds > 0)
                {
                    var pct = Math.Min(100.0, elapsed.TotalSeconds / totalSeconds * 100.0);
                    job.ProgressPercent = 50 + pct * 0.5;
                }
            });

            if (job.Options.SplitMinutes is int minutes && minutes > 0)
            {
                segmentDir = Path.Combine(_tempDir, $"segments_{jobId}");
                Directory.CreateDirectory(segmentDir);
                var pattern = Path.Combine(segmentDir, "part_%03d.mp3");

                await _converter.ConvertAsync(tempAudioPath, pattern, job.Options.BitrateKbps, minutes, conversionProgress, ct);

                job.Message = "Packaging chunks into a zip...";
                var zipPath = Path.Combine(_outputDir, $"{jobId}_{safeTitle}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(segmentDir, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

                job.OutputFilePath = zipPath;
                job.OutputFileName = $"{safeTitle}.zip";
            }
            else
            {
                var outputPath = Path.Combine(_outputDir, $"{jobId}_{safeTitle}.mp3");
                await _converter.ConvertAsync(tempAudioPath, outputPath, job.Options.BitrateKbps, null, conversionProgress, ct);

                job.OutputFilePath = outputPath;
                job.OutputFileName = $"{safeTitle}.mp3";
            }

            job.ProgressPercent = 100;
            job.Status = JobStatus.Completed;
            job.Message = "Done.";
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Canceled;
            job.Message = "Canceled.";
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.Message = "Failed.";
            _logger.LogError(ex, "Conversion failed for job {JobId}", jobId);
        }
        finally
        {
            if (tempAudioPath is not null && File.Exists(tempAudioPath))
            {
                TryDelete(() => File.Delete(tempAudioPath));
            }
            if (segmentDir is not null && Directory.Exists(segmentDir))
            {
                TryDelete(() => Directory.Delete(segmentDir, recursive: true));
            }
        }
    }

    private static void TryDelete(Action deleteAction)
    {
        try { deleteAction(); } catch { /* best-effort cleanup */ }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Length > 80 ? name[..80] : name;
    }
}
