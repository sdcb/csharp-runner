using Sdcb.CSharpRunner.Host.Hubs;
using Sdcb.CSharpRunner.Host.Mcp;
using Sdcb.CSharpRunner.Host.Services;

namespace Sdcb.CSharpRunner.Host;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<RoundRobinPool<Worker>>();
        builder.Services.AddHostedService<WorkerCountNotifier>();
        builder.Services.AddHttpClient();
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<Tools>(Tools.JsonOptions);

        WebApplication app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthorization();

        app.MapMcp("/mcp");
        app.MapControllers();
        app.MapHub<WorkerHub>("/hubs/worker");

        app.Run();
    }
}
