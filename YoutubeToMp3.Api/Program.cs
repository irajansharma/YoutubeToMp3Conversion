using Scalar.AspNetCore;
using YoutubeToMp3.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
/*builder.Services.AddEndpointsApiExplorer();*/

// Job pipeline: HTTP requests only enqueue/read state; the actual work
// happens in the singleton background service.
builder.Services.AddSingleton<IJobStore, JobStore>();
builder.Services.AddSingleton<ConversionQueue>();
builder.Services.AddHostedService<ConversionBackgroundService>();

// Permissive CORS: this is a local tool, and downloads happen via a plain
// <a href> navigation anyway (not subject to CORS), but this keeps the
// status-polling fetch calls working if the Web UI origin differs.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (!YoutubeToMp3.Api.Services.AudioConverter.IsFfmpegAvailable())
{
    app.Logger.LogWarning(
        "ffmpeg was not found on PATH. Install it (e.g. 'winget install Gyan.FFmpeg', " +
        "'brew install ffmpeg', or 'sudo apt install ffmpeg') before starting a conversion.");
}

app.UseCors();
app.MapControllers();

/*app.MapGet("/", () => Results.Ok(new
{
    service = "YoutubeToMp3.Api",
    status = "running",
    endpoints = new[]
    {
        "POST /api/convert",
        "GET  /api/convert/{jobId}",
        "GET  /api/convert/{jobId}/download",
        "DELETE /api/convert/{jobId}"
    }
}));*/
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("YoutubeToMp3 API")
            .WithTheme(ScalarTheme.Mars);
    });
}

app.Run();
