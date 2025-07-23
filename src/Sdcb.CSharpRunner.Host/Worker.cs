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
}
