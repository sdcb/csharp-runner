using System.Text.Encodings.Web;

namespace Sdcb.CSharpRunner.Worker;


internal class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

        AppSettings settings = AppSettings.Load(builder.Configuration);
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });

        WebApplication app = builder.Build();
        IHostApplicationLifetime life = app.Services.GetRequiredService<IHostApplicationLifetime>();

        app.MapGet("/", Handlers.GetHome);
        app.MapPost("/run", ctx => Handlers.Run(ctx, settings.MaxRuns, life));
        life.ApplicationStarted.Register(async () =>
        {
            if (settings.Register)
            {
                string serviceUrl = Register.GetServiceHttpUrl(app.Urls, settings.ExposedPort);
                await Register.LoginAsWorker(settings.RegisterHostUrl, serviceUrl);
            }
        });

        await app.StartAsync();
        await Handlers.Warmup();
        await app.WaitForShutdownAsync();
    }
}
