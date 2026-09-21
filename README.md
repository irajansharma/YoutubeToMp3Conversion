# YouTube to MP3 — Web API + Blazor UI

Converts long YouTube videos (including 10+ hour ones) to MP3, with a
responsive web UI. The API does the download/conversion as a background job;
the Blazor UI polls progress and gives you a download link when it's ready.

```
YoutubeToMp3Web/
├── YoutubeToMp3.Shared   Shared DTOs (request/response models, job status enum)
├── YoutubeToMp3.Api      ASP.NET Core Web API — download, ffmpeg conversion, job queue
├── YoutubeToMp3.Web      Blazor Server UI — form, live progress bar, download button
├── docs/
│   ├── ARCHITECTURE.md   How the three projects fit together, request lifecycle
│   ├── API.md            Full endpoint reference with request/response examples
│   ├── SETUP.md          Step-by-step setup from a clean machine
│   └── TROUBLESHOOTING.md  Fixes for common issues (ffmpeg, certs, stuck jobs, ...)
├── CONTRIBUTING.md       Dev workflow, code style, suggested improvements
├── CHANGELOG.md          Version history
└── README.md             You are here
```

## Prerequisites

1. **.NET 10 SDK** — https://dotnet.microsoft.com/download
2. **FFmpeg** on your system PATH:
   - Windows: `winget install Gyan.FFmpeg` (restart terminal after)
   - macOS: `brew install ffmpeg`
   - Linux: `sudo apt install ffmpeg`

   Verify with `ffmpeg -version`.

## Running it

Open **two terminals** — the API and the Web UI run as separate processes.

**Terminal 1 — API:**
```bash
cd YoutubeToMp3.Api
dotnet run
```
Runs at `https://localhost:7080` (and `http://localhost:5080`). Downloaded/converted
files are stored under `YoutubeToMp3.Api/Storage/`.

**Terminal 2 — Web UI:**
```bash
cd YoutubeToMp3.Web
dotnet run
```
Runs at `https://localhost:7090`. Open that URL in your browser.

> The Web UI's `appsettings.json` has `"ApiBaseUrl": "https://localhost:7080"`.
> If you change the API's port, update this value to match.

> First time running ASP.NET Core HTTPS locally? Trust the dev certificate once:
> `dotnet dev-certs https --trust`

## Using the UI

1. Paste a YouTube URL.
2. Pick a bitrate (192 kbps is a good default).
3. For very long videos, optionally choose a chunk size ("Split into chunks")
   — this produces a `.zip` of, say, 1-hour MP3 files instead of one giant one.
4. Click **Convert to MP3**. A progress bar tracks download (0–50%) then
   conversion (50–100%), refreshed every 2 seconds.
5. When done, click **Download** — the browser pulls the file straight from
   the API (not proxied through the Blazor server), so large files stream
   efficiently.

The layout is responsive: form fields stack vertically on narrow/mobile
screens and sit side-by-side on wider ones (see `wwwroot/css/app.css`).

## API reference

Full details, including request/response examples and a `curl` walkthrough,
are in [`docs/API.md`](docs/API.md). Quick summary:

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/convert` | Start a job. Body: `{ url, bitrateKbps, splitMinutes?, fileName? }` → `{ jobId }` |
| `GET` | `/api/convert/{jobId}` | Poll job status/progress |
| `GET` | `/api/convert/{jobId}/download` | Stream the finished MP3/ZIP |
| `DELETE` | `/api/convert/{jobId}` | Cancel an in-progress job |

You can call the API directly (e.g. from a script or Postman) without the
Blazor UI at all.

## How it handles very long (10+ hour) videos

- Downloads the **audio-only** stream (no video track) to keep size down.
- A 4-hour HTTP timeout plus automatic retry (4 attempts) absorbs the
  occasional network drop that's more common on long downloads.
- The background worker processes up to **2 jobs concurrently**
  (`SemaphoreSlim` in `ConversionBackgroundService`) so the machine isn't
  overwhelmed if multiple people queue jobs at once. Adjust the number in
  `ConversionBackgroundService` if your hardware can take more/less.
- FFmpeg streams the conversion rather than buffering the whole file in
  memory, so RAM stays flat regardless of video length.
- Optional chunk-splitting produces a `.zip` of fixed-length MP3s, which is
  easier to transfer/manage than one multi-GB file.
- Temp files (raw downloaded audio, per-job segment folders) are deleted
  automatically once a job finishes, succeeds, or fails.

## Notes / things to adjust for production use

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#known-limitations-by-design-for-a-localdev-scale-tool)
for the full list. In short: job storage is in-memory only, there's no
authentication, output files aren't auto-deleted, and CORS is wide open —
all reasonable for local/dev use, all worth tightening before wider deployment.

Setup issues? See [`docs/SETUP.md`](docs/SETUP.md) and
[`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md). Want to contribute?
See [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Legal note

Only use this on videos you own, that are public domain, or that you have
explicit permission/rights to download. Downloading copyrighted content you
don't have rights to may violate YouTube's Terms of Service and copyright law.
