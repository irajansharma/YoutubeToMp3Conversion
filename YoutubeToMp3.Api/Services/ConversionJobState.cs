using YoutubeToMp3.Shared.Models;

namespace YoutubeToMp3.Api.Services;

/// <summary>
/// Mutable server-side job record. A background service writes to this as
/// work progresses; API controllers read from it to answer status polls.
/// </summary>
public class ConversionJobState
{
    public required Guid Id { get; init; }
    public required StartConversionRequest Options { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public CancellationTokenSource Cts { get; } = new();

    public JobStatus Status { get; set; } = JobStatus.Queued;
    public double ProgressPercent { get; set; }
    public string? Title { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Message { get; set; } = "Waiting in queue...";
    public string? ErrorMessage { get; set; }
    public string? OutputFilePath { get; set; }
    public string? OutputFileName { get; set; }

    public ConversionJobDto ToDto() => new()
    {
        Id = Id,
        Status = Status,
        ProgressPercent = Math.Round(ProgressPercent, 1),
        Title = Title,
        Duration = Duration,
        Message = Message,
        ErrorMessage = ErrorMessage,
        OutputFileName = OutputFileName
    };
}
