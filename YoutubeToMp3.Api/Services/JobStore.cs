using System.Collections.Concurrent;
using YoutubeToMp3.Shared.Models;

namespace YoutubeToMp3.Api.Services;

public interface IJobStore
{
    ConversionJobState CreateJob(StartConversionRequest request);
    bool TryGet(Guid id, out ConversionJobState job);
    bool TryCancel(Guid id);
}

/// <summary>
/// Simple in-memory job registry. Fine for a single-instance local/dev
/// deployment; swap for a persistent store (DB/Redis) if scaling out.
/// </summary>
public class JobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, ConversionJobState> _jobs = new();

    public ConversionJobState CreateJob(StartConversionRequest request)
    {
        var job = new ConversionJobState { Id = Guid.NewGuid(), Options = request };
        _jobs[job.Id] = job;
        return job;
    }

    public bool TryGet(Guid id, out ConversionJobState job)
    {
        return _jobs.TryGetValue(id, out job!);
    }

    public bool TryCancel(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var job)) return false;
        job.Cts.Cancel();
        return true;
    }
}
