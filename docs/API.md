# API Reference

Base URL (default local dev): `https://localhost:7080`

All request/response bodies are JSON. All endpoints are under `/api/convert`.

---

## `POST /api/convert`

Queues a new download + conversion job.

### Request body

```json
{
  "url": "https://www.youtube.com/watch?v=XXXXXXXXXXX",
  "bitrateKbps": 192,
  "splitMinutes": null,
  "fileName": null
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `url` | string | yes | Any URL YoutubeExplode can resolve (watch URL, short `youtu.be` link, etc.) |
| `bitrateKbps` | int | no (default `192`) | Must be between 64 and 320 |
| `splitMinutes` | int? | no (default `null`) | If set, output is chunked into MP3s of this length and zipped |
| `fileName` | string? | no | Output file name without extension; defaults to the video's title (sanitized) |

### Response — `200 OK`

```json
{ "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6" }
```

### Response — `400 Bad Request`

```json
{ "error": "A YouTube URL is required." }
```

---

## `GET /api/convert/{jobId}`

Returns the current state of a job. Intended to be polled (e.g. every 2s)
while a job is in progress.

### Response — `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Converting",
  "progressPercent": 67.4,
  "title": "Some Video Title",
  "duration": "10:00:00",
  "message": "Converting to MP3...",
  "errorMessage": null,
  "outputFileName": null,
  "isReadyForDownload": false
}
```

### `status` values

| Value | Meaning |
|---|---|
| `Queued` | Accepted, waiting for a worker slot |
| `FetchingInfo` | Reading video metadata |
| `Downloading` | Pulling the audio-only stream (progress 0–50%) |
| `Converting` | Running FFmpeg (progress 50–100%) |
| `Completed` | Ready to download |
| `Failed` | See `errorMessage` |
| `Canceled` | Job was canceled via `DELETE` |

### Response — `404 Not Found`

Returned if `jobId` doesn't exist (e.g. after an API restart, since job state
is in-memory only — see [ARCHITECTURE.md](ARCHITECTURE.md)).

---

## `GET /api/convert/{jobId}/download`

Streams the finished file. Content type is `audio/mpeg` for a single MP3, or
`application/zip` when `splitMinutes` was used. Supports HTTP range requests
(`enableRangeProcessing: true`), so browsers can resume partial downloads.

### Response

- `200 OK` with the file stream, or a `206 Partial Content` for range requests.
- `404 Not Found` with `{ "error": "File is not ready yet." }` if the job
  isn't `Completed` yet, or the file is missing.

This endpoint is meant to be hit via a plain `<a href="...">` link (as the
Blazor UI does) rather than fetched via JavaScript/XHR — that way large files
stream directly to the browser's download manager without CORS concerns.

---

## `DELETE /api/convert/{jobId}`

Requests cancellation of an in-progress job (sets the job's internal
`CancellationTokenSource`). The worker checks this cooperatively at the next
opportunity (between download chunks / at FFmpeg process boundaries).

### Response

- `204 No Content` if the job existed and cancellation was requested.
- `404 Not Found` if the job doesn't exist.

Note: cancellation is best-effort. A job already deep into an FFmpeg process
will stop once the process is killed (the converter registers the
cancellation token to kill the FFmpeg process), but there's no instant abort.

---

## Example: full flow with `curl`

```bash
# 1. Start a job
curl -k -X POST https://localhost:7080/api/convert \
  -H "Content-Type: application/json" \
  -d '{"url":"https://youtu.be/XXXXXXXXXXX","bitrateKbps":192}'
# → {"jobId":"3fa85f64-..."}

# 2. Poll status
curl -k https://localhost:7080/api/convert/3fa85f64-...

# 3. Download once status is "Completed"
curl -k -OJ https://localhost:7080/api/convert/3fa85f64-.../download
```

(`-k` skips TLS verification for the local dev certificate; use
`dotnet dev-certs https --trust` instead if you'd rather trust it properly.)
