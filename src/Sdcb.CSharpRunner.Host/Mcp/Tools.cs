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

    [McpServerTool, Description("""
**工具使用指南：`run_code` C#代码执行器**

你已集成 `run_code` 工具，它能执行C#代码片段。请用它来回答需要精确计算、实时数据或复杂逻辑处理的问题，超时时间为30秒。

---
**何时使用 `run_code`：**

* **精确计算与数据处理**
    * **数学运算**:
        * 用户提问: "2的64次方减1是多少？"
        * `run_code` 代码: `(BigInteger.Pow(2, 64) - 1).ToString()`
    * **日期与时间**:
        * 用户提问: "计算一下2025年圣诞节是星期几？"
        * `run_code` 代码: `new DateTime(2025, 12, 25).DayOfWeek.ToString()`
    * **加密与哈希**:
        * 用户提问: "计算字符串 'Gemini' 的SHA256哈希值。"
        * `run_code` 代码: `string.Concat(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("Gemini")).Select(b => b.ToString("x2")))`
    * **数据转换 (LINQ)**:
        * 用户提问: "将列表 `["anna", "bob", "CATHY"]` 整理成首字母大写并排序。"
        * `run_code` 代码: `new[] { "anna", "bob", "CATHY" }.Select(n => char.ToUpper(n[0]) + n.Substring(1).ToLower()).OrderBy(n => n).ToList()`

* **文本处理与解析**
    * **JSON 操作**:
        * 用户提问: "从JSON `{\"user\": {\"name\": \"John\"}}` 中提取用户名。"
        * `run_code` 代码: `JsonDocument.Parse("{\"user\": {\"name\": \"John\"}}").RootElement.GetProperty("user").GetProperty("name").GetString()`
    * **正则表达式**:
        * 用户提问: "从文本 '我的邮箱是 test@example.com' 中提取出电子邮件地址。"
        * `run_code` 代码: `Regex.Match("我的邮箱是 test@example.com", @"[\w]+@[\w]+\.\w+").Value`

* **实时网络请求**
    * **API调用**:
        * 用户提问: "看看博客园有什么最新头条"
        * `run_code` 代码: `using (var client = new HttpClient()) { var response = await client.GetAsync("https://cnblogs.com"); return await response.Content.ReadAsStringAsync(); }`

* **算法逻辑验证**
    * **确定性问题**:
        * 用户提问: "判断1997是不是一个质数。"
        * `run_code` 代码: `int n = 1997; if (n <= 1) return false; for (int i = 2; i * i <= n; i++) { if (n % i == 0) return false; } return true;`

---
**何时避免使用 `run_code`：**

* 开放式问题（如寻求解释或建议）。
* 需要使用外部NuGet包。
* 需要跨次调用维持状态。
* 长时间运行或高资源消耗的任务。
* 需要访问本地文件或数据库。
* 需要构建用户界面（UI）。

---
**核心原则：**

* **精确优先**: 对于有确定答案的问题，优先执行代码，而不是依赖记忆。
* **任务分解**: 将复杂问题拆解，用代码解决其中的确定性步骤。
* **善用工具**: 积极利用代码执行能力，提供更可靠和实时的回答。
""")]
    public async Task<string> RunCode(string code, IProgress<ProgressNotificationValue> progress, int timeout = 30_000)
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
