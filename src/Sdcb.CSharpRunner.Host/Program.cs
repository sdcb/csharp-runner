using Sdcb.CSharpRunner.Host.Mcp;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sdcb.CSharpRunner.Host;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<RoundRobinPool<Worker>>();
        builder.Services.AddHttpClient();
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<Tools>(Tools.JsonOptions);

        WebApplication app = builder.Build();

        app.UseAuthorization();

        app.MapMcp("/mcp");
        app.MapControllers();
        app.MapRazorPages();

        app.Run();
    }
}
