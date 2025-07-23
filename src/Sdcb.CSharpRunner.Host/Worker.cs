using Sdcb.CSharpRunner.Shared;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sdcb.CSharpRunner.Host;

public record Worker : IHaveMaxRuns
{
    public required Uri Url { get; init; }
    public required int MaxRuns { get; init; }
    public int CurrentRuns { get; set; }

    internal async Task<HttpResponseMessage> Run(IHttpClientFactory http, RunCodeRequest request)
    {
        using HttpClient client = http.CreateClient();
        client.BaseAddress = Url;
        client.Timeout = TimeSpan.FromMilliseconds(request.Timeout * 2);
        using HttpRequestMessage req = new(HttpMethod.Post, "/run")
        {
            Content = JsonContent.Create(request, AppJsonContext.Default.RunCodeRequest),
        };
        HttpResponseMessage resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        return resp;
    }

    internal async IAsyncEnumerable<Memory<byte>> RunAsMemory(IHttpClientFactory http, RunCodeRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage resp = await Run(http, request);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to run code on worker {Url}. Status code: {resp.StatusCode}, Response: {await resp.Content.ReadAsStringAsync(default)}");
        }

        byte[] buffer = new byte[80 * 1024];
        using Stream stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            yield return buffer.AsMemory(0, read);
        }
    }

    internal async IAsyncEnumerable<SseResponse> RunAsJson(IHttpClientFactory http, RunCodeRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<byte> bytes = new(capacity: 80 * 1024);
        await foreach (Memory<byte> buffer in RunAsMemory(http, request, cancellationToken))
        {
            if (buffer.Span.EndsWith("\n\n"u8))
            {
                ReadOnlySpan<byte> data = [.. bytes, .. buffer.Span];
                SseResponse? json = JsonSerializer.Deserialize(data[6..], AppJsonContext.Default.SseResponse);
                if (json != null)
                {
                    yield return json;
                }
                bytes.Clear();
            }
            else
            {
                bytes.AddRange(buffer.Span);
            }
        }
    }
}
