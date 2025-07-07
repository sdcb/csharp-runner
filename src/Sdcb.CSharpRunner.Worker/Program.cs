using System.Text.Json;

namespace Sdcb.CSharpRunner.Worker;

internal class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        int maxRuns = builder.Configuration.GetValue("MAX_RUNS", 0);   // 0 = 无限
        builder.Logging.ClearProviders();

        WebApplication app = builder.Build();
        IHostApplicationLifetime life = app.Services.GetRequiredService<IHostApplicationLifetime>();

        //Handlers.Warmup();
        HttpContext fakeHttpContext = new DefaultHttpContext();
        fakeHttpContext.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
        {
            code = "Console.WriteLine(\"Ready\");"
        }));
        Handlers.Run(fakeHttpContext, 0, life).GetAwaiter().GetResult();

        // ── 3) 欢迎页 ───────────────────────────────
        app.MapGet("/", Handlers.GetHome);

        // ── 4) /run 端点 ────────────────────────────
        app.MapPost("/run", ctx => Handlers.Run(ctx, maxRuns, life));

        await app.RunAsync();
    }
}