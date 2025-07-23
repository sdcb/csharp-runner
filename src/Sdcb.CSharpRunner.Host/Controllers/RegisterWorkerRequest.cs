using Sdcb.CSharpRunner.Shared;
using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Host.Controllers;

public record RegisterWorkerRequest
{
    [JsonPropertyName("workerUrl")]
    public required Uri WorkerUrl { get; init; }

    [JsonPropertyName("maxRuns")]
    public required int MaxRuns { get; init; }

    public async Task<string?> Validate(IHttpClientFactory http)
    {
        if (MaxRuns < 0)
        {
            return "MaxRuns must be greater than 0.";
        }

        using HttpResponseMessage response = await WarmUp(http);
        if (!response.IsSuccessStatusCode)
        {
            return $"Failed to reach worker at {WorkerUrl}. Status code: {response.StatusCode}, Response: {await response.Content.ReadAsStringAsync()}";
        }

        return null;
    }

    private async Task<HttpResponseMessage> WarmUp(IHttpClientFactory http)
    {
        RunCodeRequest request = new("Console.WriteLine(\"Hello World!\");", IsWarmUp: true);
        using HttpClient client = http.CreateClient();
        client.BaseAddress = WorkerUrl;
        client.Timeout = TimeSpan.FromMilliseconds(request.Timeout * 2);
        using HttpRequestMessage req = new(HttpMethod.Post, "/run")
        {
            Content = JsonContent.Create(request, AppJsonContext.Default.RunCodeRequest),
        };
        HttpResponseMessage resp = await client.SendAsync(req);
        string content = await resp.Content.ReadAsStringAsync();
        return resp;
    }

    public Worker CreateWorker()
    {
        return new Worker()
        {
            Url = WorkerUrl,
            MaxRuns = MaxRuns,
            CurrentRuns = 0
        };
    }
}
