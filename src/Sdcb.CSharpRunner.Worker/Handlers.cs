using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Sdcb.CSharpRunner.Worker;

public static class Handlers
{
    public static IResult GetHome()
    {
        const string html = """
            <!doctype html><html lang="zh-CN">
            <head><meta charset="utf-8"><title>C# Runner is Ready</title></head>
            <body style="font-family:sans-serif">
              <h1>Sdcb.CSharpRunnerCore Worker is READY ✅</h1>
              <p>POST <code>/run</code> send <code>{"code":"your c#"}</code> to run it.</p>
            </body></html>
            """;
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static SemaphoreSlim evalLock = new(1, 1);
    private static ScriptOptions scriptOpt { get; } = ScriptOptions.Default
        .AddReferences(typeof(object).Assembly)
        .AddReferences(typeof(Enumerable).Assembly)
        .AddReferences(typeof(DataTable).Assembly)
        .AddReferences(typeof(Console).Assembly)
        .AddReferences(typeof(Thread).Assembly)
        .AddImports(
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Data",
            "System.Diagnostics",
            "System.IO",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Reflection",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Transactions",
            "System.Xml",
            "System.Xml.Linq",
            "System.Xml.XPath");
    private static int runCount = 0;


    public static async Task Run(HttpContext ctx, int maxRuns, IHostApplicationLifetime life)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Received request");
        RunCodeRequest request = await JsonSerializer.DeserializeAsync(ctx.Request.Body, AppJsonContext.Default.RunCodeRequest) 
            ?? throw new ArgumentException("Invalid request body", nameof(ctx));
        Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Deserialized request");

        // SSE 头
        ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache";

        // 并发互斥
        await evalLock.WaitAsync(ctx.RequestAborted);
        Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Acquired lock for evaluation.");
        try
        {
            Channel<SseResponse> channel = Channel.CreateUnbounded<SseResponse>();
            using CancellationTokenSource cts = new(request.Timeout);

            object? result = null;
            Exception? execErr = null;
            Exception? compilationErr = null;

            // 重定向 Console
            TextWriter oldOut = Console.Out, oldErr = Console.Error;
            ConsoleCaptureWriter outCapture = new(channel.Writer, oldOut, true);
            ConsoleCaptureWriter errCapture = new(channel.Writer, oldErr, false);
            Console.SetOut(outCapture);
            Console.SetError(errCapture);

            // ① 推流协程
            Task writerTask = Task.Run(async () =>
            {
                oldOut.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Start streaming...");
                await foreach (SseResponse msg in channel.Reader.ReadAllAsync(cts.Token))
                {
                    string json = JsonSerializer.Serialize(msg, AppJsonContext.FallbackOptions);
                    await ctx.Response.WriteAsync($"data: {json}\n\n", cts.Token);
                    await ctx.Response.Body.FlushAsync(cts.Token);
                    oldOut.WriteLine($"Elased: {sw.ElapsedMilliseconds}ms, Sent: {json}");
                }
                oldOut.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Finished streaming.");
            }, cts.Token);

            try
            {
                oldOut.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Before executing code...");
                result = await CSharpScript
                    .EvaluateAsync<object?>(request.Code, scriptOpt)
                    .WaitAsync(TimeSpan.FromMilliseconds(request.Timeout), ctx.RequestAborted);
                oldOut.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Code executed successfully.");
                if (result != null)
                {
                    channel.Writer.TryWrite(new ResultSseResponse { Result = result });
                }
            }
            catch (CompilationErrorException ex)
            {
                compilationErr = ex;
                channel.Writer.TryWrite(new CompilerErrorSseResponse
                {
                    CompilerError = ex.ToString()
                });
            }
            catch (Exception ex)
            {
                execErr = ex;
                channel.Writer.TryWrite(new ErrorSseResponse { Error = ex.ToString() });
            }
            finally
            {
                Console.SetOut(oldOut);
                Console.SetError(oldErr);

                // 结束包，带上完整 stdout/stderr
                channel.Writer.TryWrite(new EndSseResponse
                {
                    StdOutput = outCapture.CapturedText,
                    StdError = errCapture.CapturedText,
                    Result = result,
                    CompilerError = compilationErr?.ToString(),
                    Error = execErr?.ToString(),
                    Elapsed = sw.ElapsedMilliseconds
                });
                channel.Writer.Complete();
            }

            await writerTask;           // 等推流结束

            if (maxRuns != 0)
            {
                if (Interlocked.Increment(ref runCount) >= maxRuns)
                {
                    Console.WriteLine($"Max runs reached: {maxRuns}");
                    life.StopApplication();
                }
                else
                {
                    Console.WriteLine($"Run count: {runCount}/{maxRuns}");
                }
            }
        }
        finally
        {
            evalLock.Release();
        }
    }

    internal static async Task Warmup()
    {
        HttpContext fakeHttpContext = new DefaultHttpContext();
        fakeHttpContext.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new RunCodeRequest("Console.WriteLine(\"Ready\");"), AppJsonContext.Default.RunCodeRequest));
        await Run(fakeHttpContext, 0, null!);
    }
}
