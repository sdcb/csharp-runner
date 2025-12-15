using ModelContextProtocol;
using ModelContextProtocol.Server;
using Sdcb.CSharpRunner.Shared;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sdcb.CSharpRunner.Host.Mcp;

[McpServerToolType]
public class Tools(RoundRobinPool<Worker> db, IHttpClientFactory http)
{
    internal static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true, TypeInfoResolver = AppJsonContext.Default };

    [McpServerTool(Name = "run_csharp"), Description("""
C#代码执行器，用于精确计算、实时数据或复杂逻辑处理，默认超时时间30秒。

何时使用：
- 数学运算: (BigInteger.Pow(2, 64) - 1).ToString()
- 日期时间: new DateTime(2025, 12, 25).DayOfWeek.ToString()
- 加密哈希: string.Concat(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("Gemini")).Select(b => b.ToString("x2")))
- LINQ处理: new[] { "anna", "bob" }.Select(n => char.ToUpper(n[0]) + n[1..].ToLower()).OrderBy(n => n).ToList()
- JSON解析: JsonDocument.Parse("{\"name\": \"John\"}").RootElement.GetProperty("name").GetString()
- 正则匹配: Regex.Match("test@example.com", @"[\w]+@[\w]+\.\w+").Value
- HTTP请求: await new HttpClient().GetStringAsync("https://example.com")
- 算法验证: int n = 1997; for (int i = 2; i * i <= n; i++) if (n % i == 0) return false; return n > 1;

何时避免：开放式问题、外部NuGet包、状态维持、长时间任务、本地文件访问、UI构建。

原则：精确优先，任务分解，善用工具。
""")]
    public async Task<string> RunCsharp(string code, IProgress<ProgressNotificationValue> progress, int timeout = 30_000)
    {
        using RunLease<Worker> worker = await db.AcquireLeaseAsync();
        EndSseResponse endResponse = null!;
        await foreach (SseResponse buffer in worker.Value.RunAsJson(http, new RunCodeRequest(code, timeout)))
        {
            if (buffer is EndSseResponse end)
            {
                endResponse = end;
            }
            else
            {
                progress.Report(new ProgressNotificationValue()
                {
                    Message = JsonSerializer.Serialize(buffer, JsonOptions),
                    Progress = 30,
                    Total = 100,
                });
            }
        }

        return endResponse.ToFinalResponse();
    }
}
