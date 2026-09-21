# Contributing

## Project layout

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for how the three projects
fit together before making structural changes.

## Development workflow

1. `dotnet restore` at the solution root.
2. Make changes in the relevant project (`YoutubeToMp3.Shared`,
   `YoutubeToMp3.Api`, or `YoutubeToMp3.Web`).
3. If you change a DTO in `YoutubeToMp3.Shared`, rebuild both `Api` and `Web`
   since they both depend on it — a stale build in one can cause confusing
   JSON (de)serialization mismatches.
4. Run both processes (`dotnet run` in `Api`, then in `Web`) and test the
   full flow manually — there's no automated test suite yet (see
   "Suggested improvements" below).

## Code style

- Nullable reference types are enabled (`<Nullable>enable</Nullable>`) across
  all three projects — keep new code nullable-annotation-correct rather than
  suppressing warnings.
- Keep the API's `Services/` classes focused: `YoutubeDownloader` only knows
  about fetching, `AudioConverter` only knows about FFmpeg, orchestration
  lives in `ConversionBackgroundService`. Avoid merging these responsibilities.
- Progress reporting conventions: download progress is `0.0`–`1.0` (an
  `IProgress<double>` fraction), while conversion progress is reported as
  elapsed `TimeSpan` and converted to a percentage against the video's known
  duration in `ConversionBackgroundService`. Keep this consistent if you add
  new progress sources.

## Suggested improvements (good places to contribute)

- **Persistent job store**: replace `JobStore`'s `ConcurrentDictionary` with
  a real backing store (SQLite, Redis) so job state survives API restarts.
- **Automated tests**: none exist yet. `AudioConverter`'s FFmpeg
  argument-building and the `TimeRegex` progress parser are good
  unit-test candidates since they're pure logic. Integration tests around
  the controller would need a fake `IJobStore`/`ConversionQueue`.
- **Output cleanup job**: a background task that deletes files in
  `Storage/output/` older than N hours.
- **Authentication**: the API currently has none — needed before exposing it
  beyond localhost.
- **WASM front-end option**: the UI is Blazor *Server*; a Blazor WebAssembly
  version would need CORS properly configured on the API (currently
  wide-open) and a different hosting model, but the same DTOs and endpoints
  would work unchanged.

## Reporting issues

Include:
- The exact command/URL that triggered the problem.
- Full console output from the API process (most errors are logged there
  with a stack trace even if the UI shows a generic message).
- Your OS and FFmpeg version (`ffmpeg -version`).
