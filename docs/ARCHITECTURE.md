# Architecture

## Overview

The solution is split into three projects so the UI, the API, and the shared
contracts between them can evolve independently:

```
                    ┌─────────────────────────┐
                    │   YoutubeToMp3.Shared    │
                    │  (DTOs, JobStatus enum)  │
                    └────────────┬─────────────┘
                                 │ referenced by both
              ┌──────────────────┴──────────────────┐
              │                                      │
   ┌──────────▼──────────┐                ┌──────────▼──────────┐
   │  YoutubeToMp3.Web    │   HTTP calls   │  YoutubeToMp3.Api    │
   │  (Blazor Server UI)  │───────────────▶│  (ASP.NET Core Web   │
   │                      │◀───────────────│   API)                │
   └──────────────────────┘   JSON status  └──────────┬───────────┘
              ▲                                        │
              │ browser downloads file directly        │
              └────────────────────────────────────────┘
                                                         │
                                              ┌──────────▼───────────┐
                                              │ ConversionBackground  │
                                              │ Service (job worker)  │
                                              └──────────┬───────────┘
                                                          │
                                       ┌──────────────────┼──────────────────┐
                                       ▼                                     ▼
                              ┌─────────────────┐                 ┌──────────────────┐
                              │ YoutubeDownloader│                 │  AudioConverter   │
                              │  (YoutubeExplode)│                 │   (FFmpeg wrap)   │
                              └─────────────────┘                 └──────────────────┘
```

## Request lifecycle

1. **Browser → Blazor UI**: the user fills in the form and submits.
2. **Blazor UI → API** (`POST /api/convert`): the Blazor *server* (not the
   browser) makes this call via `IHttpClientFactory`. A `ConversionJobState`
   is created and its ID is pushed onto an in-memory `Channel<Guid>` queue.
3. **Background worker**: `ConversionBackgroundService` (an ASP.NET Core
   `BackgroundService`, i.e. a long-running hosted service) reads job IDs off
   the channel and processes up to 2 at a time via a `SemaphoreSlim`.
4. For each job, the worker:
   - Fetches video metadata (`YoutubeDownloader.GetVideoInfoAsync`).
   - Downloads the highest-bitrate **audio-only** stream to a temp file,
     reporting 0–100% download progress.
   - Runs FFmpeg to transcode to MP3 (or segment into chunks + zip), parsing
     FFmpeg's `stderr` output for `time=HH:MM:SS` markers to compute
     conversion progress against the video's known duration.
   - Updates the shared `ConversionJobState` object as it goes.
5. **Blazor UI polling**: while a job is active, the UI runs an async loop
   (`PollLoopAsync` in `Index.razor`) that calls `GET /api/convert/{jobId}`
   every 2 seconds and re-renders the progress bar.
6. **Download**: once `Status == Completed`, the UI renders a plain
   `<a href="{api}/api/convert/{jobId}/download">` link. Clicking it is a
   normal browser navigation straight to the API — it does **not** round-trip
   through the Blazor server, so large files stream efficiently and aren't
   held in the UI process's memory.

## Why a separate API instead of doing everything in Blazor Server?

- **Separation of concerns**: heavy CPU/IO work (FFmpeg, long downloads)
  is isolated from the UI's SignalR circuit. If the API restarts or is
  redeployed, active Blazor circuits aren't affected (beyond a failed poll,
  which the UI already tolerates).
- **Reusability**: the API can be called by anything — a script, a future
  mobile app, Postman — not just this specific UI.
- **Independent scaling**: in a real deployment you could run multiple API
  instances behind a load balancer (once the in-memory `JobStore` is swapped
  for a shared store — see "Known limitations" below) without touching the UI.

## Key components

| Component | File | Responsibility |
|---|---|---|
| `JobStore` | `Api/Services/JobStore.cs` | In-memory registry of job state, keyed by GUID |
| `ConversionQueue` | `Api/Services/ConversionQueue.cs` | Unbounded channel feeding the worker |
| `ConversionBackgroundService` | `Api/Services/ConversionBackgroundService.cs` | Consumes the queue, runs jobs with limited concurrency |
| `YoutubeDownloader` | `Api/Services/YoutubeDownloader.cs` | Wraps YoutubeExplode, adds retry + long timeout |
| `AudioConverter` | `Api/Services/AudioConverter.cs` | Wraps FFmpeg, parses progress, supports segmenting |
| `ConvertController` | `Api/Controllers/ConvertController.cs` | HTTP surface: start/status/download/cancel |
| `Index.razor` | `Web/Pages/Index.razor` | Form, polling loop, progress UI, download link |

## Known limitations (by design, for a local/dev-scale tool)

- **In-memory job state**: restarting the API loses all job history and any
  in-flight jobs. Fine for local use; would need a persistent store (SQL,
  Redis) to survive restarts or scale horizontally.
- **No authentication/authorization** on the API.
- **No automatic cleanup** of completed output files — they accumulate in
  `Api/Storage/output/` until manually deleted.
- **CORS is wide open** (`AllowAnyOrigin`) for simplicity during local dev.

See [`docs/API.md`](API.md) for the full endpoint reference and
[`docs/TROUBLESHOOTING.md`](TROUBLESHOOTING.md) for common setup issues.
