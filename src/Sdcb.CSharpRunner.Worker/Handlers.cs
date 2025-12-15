using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Sdcb.CSharpRunner.Shared;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            typeof(JsonNode).Assembly,
            typeof(SHA256).Assembly,
            typeof(BigInteger).Assembly,
            typeof(GZipStream).Assembly,
            typeof(WebUtility).Assembly,
            typeof(CultureInfo).Assembly,
            typeof(TimeZoneInfo).Assembly)
        .AddImports(
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Diagnostics",
            "System.Globalization",
            "System.IO",
            "System.IO.Compression",
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
            "System.Text.Json.Nodes",
            "System.Security",
            "System.Security.Cryptography",
            "System.Numerics");
    private static int runCount = 0;

    /// <summary>
    /// 检测代码是否为 Program 模式（包含类定义和静态 Main 方法）
    /// </summary>
    private static bool IsProgramMode(string code)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDeclarations)
        {
            var mainMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == "Main" &&
                            m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            if (mainMethods.Any())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 执行 Program 模式的代码（带有类和静态 Main 方法）
    /// </summary>
    private static async Task<object?> ExecuteProgramModeAsync(string code, CancellationToken cancellationToken)
    {
        // 为 Program 模式添加默认的 using 语句（如果代码中没有以 using 开头）
        string usings = string.Join("\n", _scriptOpt.Imports.Select(ns => $"using {ns};"));
        if (!code.TrimStart().StartsWith("using"))
        {
            code = usings + "\n\n" + code;
        }

        // 获取有效的元数据引用（过滤掉 UnresolvedMetadataReference）
        var references = _scriptOpt.MetadataReferences
            .Where(r => r is not UnresolvedMetadataReference)
            .ToList();

        // 确保基础引用存在
        var coreAssemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(Enumerable).Assembly,
            typeof(XDocument).Assembly,
            typeof(HttpClient).Assembly,
            typeof(JsonSerializer).Assembly,
            typeof(JsonNode).Assembly,
            typeof(SHA256).Assembly,
            typeof(BigInteger).Assembly,
            typeof(GZipStream).Assembly,
            typeof(WebUtility).Assembly,
            typeof(CultureInfo).Assembly,
            typeof(TimeZoneInfo).Assembly,
        };

        foreach (var asm in coreAssemblies)
        {
            var reference = MetadataReference.CreateFromFile(asm.Location);
            if (!references.Any(r => r.Display == reference.Display))
            {
                references.Add(reference);
            }
        }

        // 添加 System.Runtime 引用（.NET Core/5+ 需要）
        var runtimeAssembly = Assembly.Load("System.Runtime");
        references.Add(MetadataReference.CreateFromFile(runtimeAssembly.Location));

        var compilation = CSharpCompilation.Create(
            "DynamicAssembly_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(code) },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithAllowUnsafe(true));

        // 编译到内存
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new CompilationErrorException(string.Join("\n", errors), emitResult.Diagnostics);
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        // 查找入口点
        var entryPoint = assembly.EntryPoint 
            ?? throw new InvalidOperationException("No entry point found in the compiled assembly.");

        // 执行 Main 方法
        object? result = null;
        var parameters = entryPoint.GetParameters();
        object?[] args = parameters.Length > 0 ? new object?[] { Array.Empty<string>() } : Array.Empty<object?>();

        var invokeResult = entryPoint.Invoke(null, args);

        // 处理异步 Main 方法
        if (invokeResult is Task task)
        {
            await task.WaitAsync(cancellationToken);

            // 检查是否有返回值 (Task<T>)
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }
        }
        else if (invokeResult is ValueTask valueTask)
        {
            await valueTask.AsTask().WaitAsync(cancellationToken);
        }
        else
        {
            result = invokeResult;
        }

        return result;
    }

    /// <summary>
    /// 智能执行代码：自动检测是 Script 模式还是 Program 模式
    /// </summary>
    private static async Task<object?> ExecuteCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (IsProgramMode(code))
        {
            return await ExecuteProgramModeAsync(code, cancellationToken);
        }
        else
        {
            return await CSharpScript.EvaluateAsync<object?>(code, _scriptOpt, cancellationToken: cancellationToken);
        }
    }

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
                result = await ExecuteCodeAsync(request.Code, cts.Token);
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
