using Sdcb.CSharpRunner.Host.Mcp.Details;

namespace Sdcb.CSharpRunner.Host;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<RoundRobinPool<Worker>>();
        builder.Services.AddHttpClient();
        builder.Services.AddTransient<Mcp.Tools>();

        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();

        app.MapMcpEndpoint<Mcp.Tools>("/mcp");
        app.MapControllers();

        app.Run();
    }
}
