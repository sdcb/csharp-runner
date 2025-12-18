using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sdcb.CSharpRunner.Shared;
using Xunit;

namespace Sdcb.CSharpRunner.Worker.Tests;

public class CodeExecutionTests
{
    private async Task<EndSseResponse> RunCodeAsync(string code, int timeout = 10000)
    {
        var request = new RunCodeRequest(code, timeout);
        var jsonRequest = JsonSerializer.Serialize(request, AppJsonContext.Default.RunCodeRequest);
        
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest));
        
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await Handlers.Run(context, timeout);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("data: "))
            {
                var json = line["data: ".Length..];
                var sseResponse = JsonSerializer.Deserialize<SseResponse>(json, AppJsonContext.Default.SseResponse);

                if (sseResponse is EndSseResponse end)
                {
                    return end;
                }
            }
        }

        throw new Exception("End event not found in SSE response");
    }

    [Fact]
    public async Task SimpleExpression_ReturnsCorrectResult()
    {
        var result = await RunCodeAsync("1 + 2");
        Assert.Equal(3, ((JsonElement)result.Result!).GetInt32());
    }

    [Fact]
    public async Task ConsoleWriteLine_ReturnsCorrectOutput()
    {
        var result = await RunCodeAsync("Console.WriteLine(\"Hello Script!\");");
        Assert.Contains("Hello Script!", result.StdOutput);
    }

    [Fact]
    public async Task MultiLineWithResult_ReturnsCorrectResultAndOutput()
    {
        var code = @"
Console.WriteLine(""Calculating..."");
int a = 10;
int b = 20;
int result = a + b;
Console.WriteLine($""Result: {result}"");
result";
        var result = await RunCodeAsync(code);
        Assert.Equal(30, ((JsonElement)result.Result!).GetInt32());
        Assert.Contains("Result: 30", result.StdOutput);
    }

    [Fact]
    public async Task LinqExpression_ReturnsCorrectResult()
    {
        var result = await RunCodeAsync("Enumerable.Range(1, 5).Sum()");
        Assert.Equal(15, ((JsonElement)result.Result!).GetInt32());
    }

    [Fact]
    public async Task VoidMain_ReturnsCorrectOutput()
    {
        var code = @"
public class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello from void Main!"");
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Contains("Hello from void Main!", result.StdOutput);
    }

    [Fact]
    public async Task IntMain_ReturnsCorrectResultAndOutput()
    {
        var code = @"
public class Program
{
    public static int Main()
    {
        Console.WriteLine(""Hello from int Main!"");
        return 42;
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Equal(42, ((JsonElement)result.Result!).GetInt32());
        Assert.Contains("Hello from int Main!", result.StdOutput);
    }

    [Fact]
    public async Task MainWithArgs_ReturnsCorrectOutput()
    {
        var code = @"
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine($""Args count: {args.Length}"");
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Contains("Args count: 0", result.StdOutput);
    }

    [Fact]
    public async Task AsyncMain_ReturnsCorrectOutput()
    {
        var code = @"
public class Program
{
    public static async Task Main()
    {
        await Task.Delay(50);
        Console.WriteLine(""Hello from async Main!"");
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Contains("Hello from async Main!", result.StdOutput);
    }

    [Fact]
    public async Task AsyncIntMain_ReturnsCorrectResultAndOutput()
    {
        var code = @"
public class Program
{
    public static async Task<int> Main()
    {
        await Task.Delay(50);
        Console.WriteLine(""Hello from async Task<int> Main!"");
        return 123;
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Equal(123, ((JsonElement)result.Result!).GetInt32());
        Assert.Contains("Hello from async Task<int> Main!", result.StdOutput);
    }

    [Fact]
    public async Task ProgramWithUsings_ReturnsCorrectOutput()
    {
        var code = @"
using System;
using System.Linq;

public class Program
{
    public static void Main()
    {
        var sum = Enumerable.Range(1, 10).Sum();
        Console.WriteLine($""Sum 1-10: {sum}"");
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Contains("Sum 1-10: 55", result.StdOutput);
    }

    [Fact]
    public async Task HttpClientTest_ReturnsOkStatus()
    {
        var code = @"
using System;
using System.Net.Http;
using System.Threading.Tasks;

public static class Program
{
    public static async Task Main()
    {
        new Uri(""https://www.example.com"");
        await Task.Delay(0);
    }
}";
        var result = await RunCodeAsync(code);
        Assert.Null(result.Error);
    }
}
