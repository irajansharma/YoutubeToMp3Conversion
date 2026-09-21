
using YoutubeToMp3.Shared.Models;

namespace YoutubeToMp3.Web.Pages
{
    public partial class Index
    {
        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private class ConversionForm
        {
            public string Url { get; set; } = string.Empty;
            public int BitrateKbps { get; set; } = 192;
            public int SplitMinutesOption { get; set; } = 0;
            public string? FileName { get; set; }
        }

        private readonly ConversionForm _form = new();
        private ConversionJobDto? _job;
        private Guid? _jobId;
        private string? _errorMessage;
        private CancellationTokenSource? _pollCts;

        private bool IsBusy => _job is not null &&
            _job.Status is JobStatus.Queued or JobStatus.FetchingInfo or JobStatus.Downloading or JobStatus.Converting;

        private string DownloadUrl => $"{Configuration["ApiBaseUrl"]}/api/convert/{_jobId}/download";

        private async Task StartConversion()
        {
            _errorMessage = null;
            _job = null;

            if (string.IsNullOrWhiteSpace(_form.Url))
            {
                _errorMessage = "Please enter a YouTube URL.";
                return;
            }

            var client = HttpClientFactory.CreateClient("Api");
            var request = new StartConversionRequest
            {
                Url = _form.Url,
                BitrateKbps = _form.BitrateKbps,
                SplitMinutes = _form.SplitMinutesOption > 0 ? _form.SplitMinutesOption : null,
                FileName = string.IsNullOrWhiteSpace(_form.FileName) ? null : _form.FileName
            };

            try
            {
                var response = await client.PostAsJsonAsync("api/convert", request);
                if (!response.IsSuccessStatusCode)
                {
                    var problem = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Could not start conversion: {problem}";
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<StartResponse>(JsonOptions);
                _jobId = result!.JobId;
                _job = new ConversionJobDto { Id = _jobId.Value, Status = JobStatus.Queued, Message = "Queued..." };

                StartPolling();
            }
            catch (HttpRequestException ex)
            {
                _errorMessage = $"Could not reach the API. Is it running? ({ex.Message})";
            }
        }

        private void StartPolling()
        {
            _pollCts?.Cancel();
            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;
            var jobId = _jobId!.Value;

            _ = PollLoopAsync(jobId, token);
        }

        private async Task PollLoopAsync(Guid jobId, CancellationToken token)
        {
            var client = HttpClientFactory.CreateClient("Api");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var dto = await client.GetFromJsonAsync<ConversionJobDto>($"api/convert/{jobId}", JsonOptions, token);
                    if (dto is not null)
                    {
                        _job = dto;
                        await InvokeAsync(StateHasChanged);

                        if (dto.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Canceled)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // transient poll failure — keep trying until canceled
                }

                await Task.Delay(TimeSpan.FromSeconds(2), token).ContinueWith(_ => { });
            }
        }

        private async Task CancelJob()
        {
            if (_jobId is null) return;

            var client = HttpClientFactory.CreateClient("Api");
            await client.DeleteAsync($"api/convert/{_jobId}");
        }

        private static string FormatDuration(TimeSpan d) =>
            d.TotalHours >= 1 ? $"{(int)d.TotalHours}h {d.Minutes}m {d.Seconds}s" : $"{d.Minutes}m {d.Seconds}s";

        private class StartResponse
        {
            public Guid JobId { get; set; }
        }

        public void Dispose()
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        }
    }
}