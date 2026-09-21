namespace YoutubeToMp3.Shared.Models;

public enum JobStatus
{
    Queued,
    FetchingInfo,
    Downloading,
    Converting,
    Completed,
    Failed,
    Canceled
}
