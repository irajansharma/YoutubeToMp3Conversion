using Microsoft.AspNetCore.Mvc;
using YoutubeToMp3.Api.Services;
using YoutubeToMp3.Shared.Models;

namespace YoutubeToMp3.Api.Controllers;

[ApiController]
[Route("api/convert")]
public class ConvertController : ControllerBase
{
    private readonly IJobStore _jobStore;
    private readonly ConversionQueue _queue;

    public ConvertController(IJobStore jobStore, ConversionQueue queue)
    {
        _jobStore = jobStore;
        _queue = queue;
    }

    /// <summary>Queues a new download+conversion job. Returns the job ID to poll.</summary>
    [HttpPost]
    public ActionResult<object> Start([FromBody] StartConversionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { error = "A YouTube URL is required." });
        }

        if (request.BitrateKbps is < 64 or > 320)
        {
            return BadRequest(new { error = "Bitrate must be between 64 and 320 kbps." });
        }

        var job = _jobStore.CreateJob(request);
        _queue.Enqueue(job.Id);

        return Ok(new { jobId = job.Id });
    }

    /// <summary>Returns current status/progress for a job.</summary>
    [HttpGet("{jobId:guid}")]
    public ActionResult<ConversionJobDto> GetStatus(Guid jobId)
    {
        if (!_jobStore.TryGet(jobId, out var job))
        {
            return NotFound();
        }

        return Ok(job.ToDto());
    }

    /// <summary>Streams the finished MP3 (or ZIP of chunks) to the caller.</summary>
    [HttpGet("{jobId:guid}/download")]
    public IActionResult Download(Guid jobId)
    {
        if (!_jobStore.TryGet(jobId, out var job))
        {
            return NotFound();
        }

        if (job.Status != JobStatus.Completed || job.OutputFilePath is null || !System.IO.File.Exists(job.OutputFilePath))
        {
            return NotFound(new { error = "File is not ready yet." });
        }

        var contentType = job.OutputFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? "application/zip"
            : "audio/mpeg";

        var stream = new FileStream(job.OutputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType, job.OutputFileName, enableRangeProcessing: true);
    }

    /// <summary>Cancels an in-progress job.</summary>
    [HttpDelete("{jobId:guid}")]
    public IActionResult Cancel(Guid jobId)
    {
        return _jobStore.TryCancel(jobId) ? NoContent() : NotFound();
    }
}
