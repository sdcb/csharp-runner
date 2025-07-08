using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sdcb.CSharpRunner.Worker;


internal class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

        int maxRuns = builder.Configuration.GetValue("MAX_RUNS", 0);   // 0 = unlimited
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });

        WebApplication app = builder.Build();
        IHostApplicationLifetime life = app.Services.GetRequiredService<IHostApplicationLifetime>();

        app.MapGet("/", Handlers.GetHome);
        app.MapPost("/run", ctx => Handlers.Run(ctx, maxRuns, life));
        _ = Handlers.Warmup();

        await app.RunAsync();
    }
}
