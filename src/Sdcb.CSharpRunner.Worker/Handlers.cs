using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Sdcb.CSharpRunner.Shared;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using System.Xml.Linq;

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

    private static readonly SemaphoreSlim _evalLock = new(1, 1);
    private static readonly ScriptOptions _scriptOpt = ScriptOptions.Default
        .AddReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Console).Assembly,
            typeof(Thread).Assembly,
            typeof(XDocument).Assembly,
            typeof(Task).Assembly,
            typeof(ValueTask).Assembly,
            typeof(HttpClient).Assembly,
            typeof(JsonSerializer).Assembly,
            typeof(SHA256).Assembly,
            typeof(BigInteger).Assembly)
        .AddImports(
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Diagnostics",
            "System.IO",
            "System.Linq",
            "System.Reflection",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Xml",
            "System.Xml.Linq",
            "System.Xml.XPath",
            "System.Threading.Tasks",
            "System.Collections.Concurrent",
            "System.Net",
            "System.Net.Http",
            "System.Text.Json",
            "System.Security",
            "System.Security.Cryptography",
            "System.Numerics");
    private static int runCount = 0;

    public static async Task Run(HttpContext ctx, int maxTimeout = 30_000, int maxRuns = 0, IHostApplicationLifetime? life = null)
    {
        Stopwatch sw = Stopwatch.StartNew();
        RunCodeRequest request = await JsonSerializer.DeserializeAsync(ctx.Request.Body, AppJsonContext.Default.RunCodeRequest)
            ?? throw new ArgumentException("Invalid request body", nameof(ctx));
        Console.WriteLine($"Recieved request, elapsed: {sw.ElapsedMilliseconds}ms, timeout: {request.Timeout}, Code: \n{request.Code}");

        // SSE 头
        ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache";

        // 并发互斥
        await _evalLock.WaitAsync(ctx.RequestAborted);
        try
        {
            Channel<SseResponse> channel = Channel.CreateUnbounded<SseResponse>();

            object? result = null;
            string? execErr = null;
            Exception? compilationErr = null;

            // 重定向 Console
            TextWriter oldOut = Console.Out, oldErr = Console.Error;
            ConsoleCaptureWriter outCapture = new(channel.Writer, true);
            ConsoleCaptureWriter errCapture = new(channel.Writer, false);
            Console.SetOut(outCapture);
            Console.SetError(errCapture);

            // ① 推流协程
            Task writerTask = Task.Run(async () =>
            {
                await foreach (SseResponse msg in channel.Reader.ReadAllAsync(default))
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(msg, AppJsonContext.FallbackOptions);
                        if (json.Length > 5 * 1024 * 1024)
                        {
                            throw new Exception("SSE message too large, please reduce the output size.");
                        }
                        await ctx.Response.WriteAsync($"data: {json}\n\n", default);
                        await ctx.Response.Body.FlushAsync(default);
                        oldOut.WriteLine($"Elased: {sw.ElapsedMilliseconds}ms, Sent: {json}");
                    }
                    catch (Exception ex)
                    {
                        oldErr.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, error writing SSE: {ex}");
                        if (msg is EndSseResponse)
                        {
                            EndSseResponse newEnd = new()
                            {
                                Error = "Error writing SSE: " + ex.Message,
                                Elapsed = sw.ElapsedMilliseconds
                            };
                            string json = JsonSerializer.Serialize(msg, AppJsonContext.FallbackOptions);
                            await ctx.Response.WriteAsync($"data: {json}\n\n", default);
                            await ctx.Response.Body.FlushAsync(default);
                        }
                    }
                }
                oldOut.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms, Finished streaming.");
            }, default);

            try
            {
                using CancellationTokenSource cts = new(Math.Min(maxTimeout, request.Timeout));
                result = await CSharpScript
                    .EvaluateAsync<object?>(request.Code, _scriptOpt, cancellationToken: cts.Token);
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
            catch (OperationCanceledException)
            {
                execErr = "Execution timed out after " + request.Timeout + "ms.";
                channel.Writer.TryWrite(new ErrorSseResponse { Error = execErr });
            }
            catch (Exception ex)
            {
                execErr = ex.ToString();
                channel.Writer.TryWrite(new ErrorSseResponse { Error = execErr });
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

            if (!request.IsWarmUp && maxRuns != 0)
            {
                if (Interlocked.Increment(ref runCount) >= maxRuns)
                {
                    Console.WriteLine($"Max runs reached: {maxRuns}");
                    life?.StopApplication();
                }
                else
                {
                    Console.WriteLine($"Run count: {runCount}/{maxRuns}");
                }
            }
        }
        finally
        {
            _evalLock.Release();
        }
    }

    internal static async Task Warmup()
    {
        HttpContext fakeHttpContext = new DefaultHttpContext();
        fakeHttpContext.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new RunCodeRequest("Console.WriteLine(\"Ready\");"), AppJsonContext.Default.RunCodeRequest));
        await Run(fakeHttpContext);
    }
}
