# Changelog

All notable changes to this project are documented here.
Format loosely follows [Keep a Changelog](https://keepachangelog.com/).

## [1.0.0] — Initial release

### Added
- `YoutubeToMp3.Shared`: shared DTOs (`StartConversionRequest`,
  `ConversionJobDto`) and `JobStatus` enum used by both API and UI.
- `YoutubeToMp3.Api`: ASP.NET Core Web API with:
  - `POST /api/convert`, `GET /api/convert/{id}`,
    `GET /api/convert/{id}/download`, `DELETE /api/convert/{id}` endpoints.
  - Background job processing (`ConversionBackgroundService`) with 2
    concurrent job slots via `SemaphoreSlim`.
  - `YoutubeDownloader`: audio-only stream download via YoutubeExplode, with
    a 4-hour HTTP timeout and 4-attempt retry for long/large downloads.
  - `AudioConverter`: FFmpeg wrapper supporting single-file MP3 output or
    fixed-length chunked output (zipped), with progress parsed from FFmpeg's
    `stderr` (`time=HH:MM:SS` markers).
  - In-memory `JobStore` for job state (see known limitations in
    `docs/ARCHITECTURE.md`).
- `YoutubeToMp3.Web`: Blazor Server UI with:
  - Conversion form (URL, bitrate, optional chunk-splitting, custom filename).
  - Live progress polling every 2 seconds with a visual progress bar.
  - Direct-to-API download link (bypasses the Blazor server for the actual
    file transfer).
  - Responsive CSS (`wwwroot/css/app.css`) — form fields stack on narrow
    screens, sit side-by-side on wider ones.
- Documentation: `README.md`, `docs/ARCHITECTURE.md`, `docs/API.md`,
  `docs/SETUP.md`, `docs/TROUBLESHOOTING.md`, `CONTRIBUTING.md`.

### Known limitations
- Job state is in-memory only (lost on API restart).
- No authentication on the API.
- Completed output files are not automatically cleaned up.
- CORS on the API is fully open (`AllowAnyOrigin`) — intended for local/dev use.

[1.0.0]: #100--initial-release
