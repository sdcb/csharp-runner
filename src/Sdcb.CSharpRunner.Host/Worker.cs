using Sdcb.CSharpRunner.Shared;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Sdcb.CSharpRunner.Host;

public class Worker : IHaveMaxRuns
{
    public required Uri Url { get; init; }
    public required int MaxRuns { get; init; }
    public int CurrentRuns { get; set; }

    internal async Task<HttpResponseMessage> Run(IHttpClientFactory http, RunCodeRequest request)
    {
        using HttpClient client = http.CreateClient();
        client.BaseAddress = Url;
        client.Timeout = TimeSpan.FromMilliseconds(Math.Max(request.Timeout * 2, 30_000));
        using HttpRequestMessage req = new(HttpMethod.Post, "/run")
        {
            Content = JsonContent.Create(request, AppJsonContext.Default.RunCodeRequest),
        };
        HttpResponseMessage resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        return resp;
    }

    internal async IAsyncEnumerable<SseResponse> RunAsJson(IHttpClientFactory http, RunCodeRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage resp = await Run(http, request);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to run code on worker {Url}. Status code: {resp.StatusCode}, Response: {await resp.Content.ReadAsStringAsync(default)}");
        }

        using Stream stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        SseParser<SseResponse> sse = SseParser.Create(stream, (type, data) => JsonSerializer.Deserialize(data, AppJsonContext.Default.SseResponse)!);
        await foreach (SseItem<SseResponse> item in sse.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }

    public override int GetHashCode() => Url.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (obj is Worker other)
        {
            return Url == other.Url;
        }
        return false;
    }
}
