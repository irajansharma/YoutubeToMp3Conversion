using System.Diagnostics;
using System.Text.RegularExpressions;

namespace YoutubeToMp3.Api.Services;

public partial class AudioConverter
{
    public static bool IsFfmpegAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts input audio to MP3. If segmentMinutes is set, produces numbered
    /// chunk files at outputPathOrPattern (which must contain a printf-style
    /// %03d placeholder) instead of one single file.
    /// </summary>
    public async Task ConvertAsync(
        string inputPath,
        string outputPathOrPattern,
        int bitrateKbps,
        int? segmentMinutes,
        IProgress<TimeSpan>? elapsedProgress,
        CancellationToken ct)
    {
        string arguments = segmentMinutes is int minutes && minutes > 0
            ? $"-i \"{inputPath}\" -vn -ar 44100 -ac 2 -b:a {bitrateKbps}k -f segment " +
              $"-segment_time {minutes * 60} -reset_timestamps 1 \"{outputPathOrPattern}\" -y"
            : $"-i \"{inputPath}\" -vn -ar 44100 -ac 2 -b:a {bitrateKbps}k \"{outputPathOrPattern}\" -y";

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            var match = TimeRegex().Match(e.Data);
            if (match.Success)
            {
                var elapsed = new TimeSpan(0,
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    (int)double.Parse(match.Groups[3].Value));
                elapsedProgress?.Report(elapsed);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        using (ct.Register(() => TryKill(process)))
        {
            await process.WaitForExitAsync(ct);
        }

        if (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.");
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(true); } catch { /* already exited */ }
    }

    // Matches ffmpeg's stderr progress lines, e.g. "time=01:23:45.67"
    [GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2}(?:\.\d+)?)")]
    private static partial Regex TimeRegex();
}
