namespace YoutubeToMp3.Shared.Models;

public class ConversionJobDto
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }

    /// <summary>0-100. First 50% is download, last 50% is conversion.</summary>
    public double ProgressPercent { get; set; }

    public string? Title { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputFileName { get; set; }
    public bool IsReadyForDownload => Status == JobStatus.Completed && OutputFileName is not null;
}
