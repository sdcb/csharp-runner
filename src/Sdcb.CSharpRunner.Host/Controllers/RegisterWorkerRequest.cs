using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Host;

public record RegisterWorkerRequest
{
    [JsonPropertyName("workerUrl")]
    public required string WorkerUrl { get; init; }

    [JsonPropertyName("maxRuns")]
    public required int MaxRuns { get; init; }

    public async Task<string?> Validate(IHttpClientFactory http)
    {
        if (MaxRuns < 0)
        {
            return "MaxRuns must be greater than 0.";
        }

        using HttpClient client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        HttpResponseMessage response = await client.GetAsync(WorkerUrl);
        if (!response.IsSuccessStatusCode)
        {
            return $"Failed to reach worker at {WorkerUrl}. Status code: {response.StatusCode}";
        }

        return null;
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
