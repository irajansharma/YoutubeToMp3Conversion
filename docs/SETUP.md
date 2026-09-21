# Setup Guide

Step-by-step instructions to get the project running from a clean machine.

## 1. Install prerequisites

| Tool | Check | Install |
|---|---|---|
| .NET 10 SDK | `dotnet --version` → `10.x` | https://dotnet.microsoft.com/download |
| FFmpeg | `ffmpeg -version` | Windows: `winget install Gyan.FFmpeg`<br>macOS: `brew install ffmpeg`<br>Linux: `sudo apt install ffmpeg` |

If either command isn't found, install the missing tool and **restart your
terminal** before continuing (PATH changes usually need a fresh shell).

## 2. Trust the local HTTPS dev certificate (one-time)

Both projects run on `https://localhost:...` in development. Trust the .NET
dev certificate so your browser doesn't warn about it:

```bash
dotnet dev-certs https --trust
```

On Linux, certificate trust works differently per distro — if `--trust`
doesn't apply automatically, you can instead run the API on its `http://`
endpoint during local testing (see `appsettings.json`) and update
`YoutubeToMp3.Web/appsettings.json`'s `ApiBaseUrl` to match.

## 3. Restore and build

From the solution root (`YoutubeToMp3Web/`):

```bash
dotnet restore
dotnet build
```

This restores NuGet packages (`YoutubeExplode` for the API) for all three
projects and confirms everything compiles.

## 4. Run the API

```bash
cd YoutubeToMp3.Api
dotnet run
```

You should see log output ending in something like:

```
Now listening on: https://localhost:7080
Now listening on: http://localhost:5080
```

Leave this terminal running. Storage folders (`Storage/temp`,
`Storage/output`) are created automatically on first run inside
`YoutubeToMp3.Api/`.

> If you see a warning about `ffmpeg was not found on PATH`, go back to
> step 1 — conversions will fail until FFmpeg is installed and on PATH.

## 5. Run the Web UI

In a **second terminal**:

```bash
cd YoutubeToMp3.Web
dotnet run
```

You should see:

```
Now listening on: https://localhost:7090
```

Open `https://localhost:7090` in your browser.

## 6. Verify end-to-end

1. Paste a short YouTube URL (test with something under a minute first).
2. Leave settings at default (192 kbps, single file).
3. Click **Convert to MP3**.
4. Watch the progress bar move through Downloading → Converting → Completed.
5. Click **Download** and confirm the MP3 plays.

Once that works, try a long video with `--split` (chunking) enabled from the
UI's "Split into chunks" dropdown to confirm the zip path works too.

## 7. (Optional) Change ports

If `7080`/`7090` conflict with something else on your machine:

1. Edit `YoutubeToMp3.Api/appsettings.json` → `Kestrel:Endpoints` URLs.
2. Edit `YoutubeToMp3.Web/appsettings.json` → `ApiBaseUrl` to match the new
   API port.

## 8. (Optional) Publish a self-contained build

```bash
cd YoutubeToMp3.Api
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

cd ../YoutubeToMp3.Web
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Swap `win-x64` for `linux-x64` / `osx-x64` as needed. Note the Web project
still needs the API reachable at whatever `ApiBaseUrl` it's configured with,
so publish/deploy both together and keep that setting in sync.

Having trouble? See [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md).
