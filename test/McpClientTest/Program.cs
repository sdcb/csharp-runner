using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var mcpUrl = "http://localhost:5050/mcp";

Console.WriteLine("=== MCP Client Test - run_csharp ===\n");

// 定义所有测试用例
var testCases = new (string Name, string Code)[]
{
    ("数学运算 - BigInteger", @"(BigInteger.Pow(2, 64) - 1).ToString()"),
    ("日期时间 - DayOfWeek", @"new DateTime(2025, 12, 25).DayOfWeek.ToString()"),
    ("加密哈希 - SHA256", @"string.Concat(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(""Gemini"")).Select(b => b.ToString(""x2"")))"),
    ("LINQ处理 - 首字母大写排序", @"new[] { ""anna"", ""bob"" }.Select(n => char.ToUpper(n[0]) + n[1..].ToLower()).OrderBy(n => n).ToList()"),
    ("JSON解析 - GetProperty", @"JsonDocument.Parse(""{\""name\"": \""John\""}"").RootElement.GetProperty(""name"").GetString()"),
    ("正则匹配 - Email提取", @"Regex.Match(""test@example.com"", @""[\w]+@[\w]+\.\w+"").Value"),
    ("算法验证 - 质数判断", @"int n = 1997; for (int i = 2; i * i <= n; i++) if (n % i == 0) return false; return n > 1;"),
};

try
{
    // 创建 MCP 客户端
    var transport = new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpUrl),
        Name = "TestClient"
    });

    await using var client = await McpClient.CreateAsync(transport);

    Console.WriteLine($"已连接到 MCP 服务器: {mcpUrl}\n");

    // 获取可用工具列表
    var tools = await client.ListToolsAsync();
    Console.WriteLine("可用工具:");
    string? toolName = null;
    foreach (var tool in tools)
    {
        Console.WriteLine($"  - {tool.Name}");
        // 使用找到的第一个工具（可能是 run_code 或 run_csharp）
        if (toolName == null && (tool.Name == "run_csharp" || tool.Name == "run_code"))
        {
            toolName = tool.Name;
        }
    }
    Console.WriteLine();

    if (toolName == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("未找到可用的代码执行工具");
        Console.ResetColor();
        return 1;
    }

    Console.WriteLine($"使用工具: {toolName}\n");

    // 运行所有测试用例
    int passed = 0;
    int failed = 0;

    foreach (var (name, code) in testCases)
    {
        Console.WriteLine($"--- 测试: {name} ---");
        Console.WriteLine($"代码: {code}");

        try
        {
            var result = await client.CallToolAsync(
                toolName,
                new Dictionary<string, object?>
                {
                    ["code"] = code
                });

            var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
            if (textContent != null)
            {
                Console.WriteLine($"结果: {textContent.Text}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ 通过");
                Console.ResetColor();
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ 无返回内容");
                Console.ResetColor();
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ 失败: {ex.Message}");
            Console.ResetColor();
            failed++;
        }

        Console.WriteLine();
    }

    // 汇总结果
    Console.WriteLine("=== 测试汇总 ===");
    Console.WriteLine($"总计: {testCases.Length}");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"通过: {passed}");
    Console.ResetColor();
    if (failed > 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"失败: {failed}");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"连接失败: {ex.Message}");
    Console.WriteLine($"请确保 MCP 服务器正在运行: {mcpUrl}");
    Console.ResetColor();
    return 1;
}

return 0;
