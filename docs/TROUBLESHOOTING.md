# Troubleshooting

## "ffmpeg was not found on PATH" (API startup warning)

The API logs this warning at startup but still starts — conversions will
fail until FFmpeg is installed and importantly **on PATH**.

- Confirm with `ffmpeg -version` in the *same terminal* you'll run
  `dotnet run` from.
- On Windows, after `winget install Gyan.FFmpeg`, you usually need to open a
  **new** terminal window for the updated PATH to take effect.
- If it's installed but not on PATH, either add its folder to your PATH
  environment variable, or hardcode the full path to the executable in
  `AudioConverter.cs` (`FileName = "ffmpeg"` → `FileName = @"C:\path\to\ffmpeg.exe"`).

## Browser shows a certificate warning on `https://localhost:...`

Run:
```bash
dotnet dev-certs https --trust
```
then restart both `dotnet run` processes and refresh the browser tab.

## "Could not reach the API. Is it running?" in the Blazor UI

- Confirm the API terminal is still running and didn't crash — check its
  console output for exceptions.
- Confirm `YoutubeToMp3.Web/appsettings.json`'s `ApiBaseUrl` matches the
  actual port the API is listening on (check the API's own startup log).
- If you changed the API's port in its `appsettings.json`, update the Web
  project's `ApiBaseUrl` to match and restart the Web process.

## Job gets stuck at "Queued" and never progresses

- Check the API console for exceptions — a crash in
  `ConversionBackgroundService` for one job shouldn't block others, but a
  startup-level failure (e.g. the hosted service never started) would.
- Confirm you didn't already queue 2 other long-running jobs — the worker
  processes at most 2 concurrently (`SemaphoreSlim(2)` in
  `ConversionBackgroundService`). A third job will sit in `Queued` until a
  slot frees up. Increase the semaphore count if your machine can handle it.

## Download fails with 404 / "File is not ready yet"

- The job must be in `Completed` status first — check
  `GET /api/convert/{jobId}` and confirm `status: "Completed"`.
- If the API process restarted between conversion finishing and you clicking
  download, the in-memory job record is gone even if the file still exists
  on disk (see the in-memory job-store limitation in
  [ARCHITECTURE.md](ARCHITECTURE.md)). You'd need to look directly in
  `YoutubeToMp3.Api/Storage/output/` for the file in that case.

## Video download fails / YoutubeExplode throws an error

- YouTube periodically changes internals in ways that can break stream
  extraction libraries. Check for a newer `YoutubeExplode` NuGet version:
  ```bash
  cd YoutubeToMp3.Api
  dotnet add package YoutubeExplode
  ```
- Age-restricted, region-locked, or private videos may not be downloadable
  without additional authentication that this project doesn't implement.
- Livestreams and premieres that haven't finished yet generally aren't
  supported.

## Conversion is very slow

- FFmpeg transcoding speed depends heavily on CPU. A 10-hour audio file at
  192 kbps typically transcodes much faster than real-time on a modern CPU,
  but a heavily loaded or low-power machine can be slower.
- Two jobs run concurrently by default; if you're running one job alone and
  it's still slow, check Task Manager / `top` for CPU contention from other
  processes.

## Disk fills up over time

Output files in `YoutubeToMp3.Api/Storage/output/` are **not** automatically
deleted after download (see the "Known limitations" note in
[ARCHITECTURE.md](ARCHITECTURE.md)). Periodically clear old files manually,
or add a scheduled cleanup task if you're running this long-term.

## Still stuck?

Check the API's console output first — most failures (download errors,
FFmpeg exit codes, unhandled exceptions) are logged there with a stack trace,
even when the Web UI only shows a generic error message.
