# C# Runner 代码执行测试脚本
# 用于验证 Script 模式和 Program 模式是否正常工作

param(
    [string]$BaseUrl = "http://localhost:5105",
    [int]$Timeout = 10000
)

$ErrorActionPreference = "Stop"

function Write-TestHeader($title) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $title" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-TestResult($success, $message) {
    if ($success) {
        Write-Host "✅ PASS: $message" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $message" -ForegroundColor Red
    }
}

function Invoke-CodeTest {
    param(
        [string]$TestName,
        [string]$Code,
        [object]$ExpectedResult = $null,
        [string]$ExpectedOutput = $null
    )

    Write-Host "`n--- $TestName ---" -ForegroundColor Yellow
    
    try {
        $body = @{
            code = $Code
            timeout = $Timeout
        } | ConvertTo-Json -Compress
        
        $response = Invoke-WebRequest -Uri "$BaseUrl/run" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing
        $content = $response.Content
        
        # 解析 SSE 响应，找到 end 事件
        $lines = $content -split "`n" | Where-Object { $_ -match "^data: " }
        $endLine = $lines | Where-Object { $_ -match '"kind":"end"' } | Select-Object -Last 1
        
        if (-not $endLine) {
            Write-TestResult $false "未找到结束响应"
            return $false
        }
        
        $endData = ($endLine -replace "^data: ", "") | ConvertFrom-Json
        
        # 检查是否有错误
        if ($endData.error -or $endData.compilerError) {
            Write-Host "Error: $($endData.error)$($endData.compilerError)" -ForegroundColor Red
            Write-TestResult $false "执行出错"
            return $false
        }
        
        $success = $true
        
        # 验证返回值
        if ($null -ne $ExpectedResult) {
            if ($endData.result -eq $ExpectedResult) {
                Write-Host "  返回值: $($endData.result) (预期: $ExpectedResult)" -ForegroundColor Gray
            } else {
                Write-Host "  返回值: $($endData.result) (预期: $ExpectedResult)" -ForegroundColor Red
                $success = $false
            }
        }
        
        # 验证输出
        if ($null -ne $ExpectedOutput) {
            $actualOutput = $endData.stdOutput -replace "`r`n", "`n" -replace "`n$", ""
            $expectedNormalized = $ExpectedOutput -replace "`r`n", "`n" -replace "`n$", ""
            if ($actualOutput -like "*$expectedNormalized*") {
                Write-Host "  输出包含: $ExpectedOutput" -ForegroundColor Gray
            } else {
                Write-Host "  输出: $actualOutput" -ForegroundColor Red
                Write-Host "  预期包含: $ExpectedOutput" -ForegroundColor Red
                $success = $false
            }
        }
        
        Write-Host "  耗时: $($endData.elapsed)ms" -ForegroundColor Gray
        Write-TestResult $success $TestName
        return $success
    }
    catch {
        Write-Host "  异常: $_" -ForegroundColor Red
        Write-TestResult $false $TestName
        return $false
    }
}

# 开始测试
Write-Host "`n🚀 C# Runner 代码执行测试" -ForegroundColor Magenta
Write-Host "目标地址: $BaseUrl" -ForegroundColor Gray

$totalTests = 0
$passedTests = 0

# ============================================
# Script 模式测试
# ============================================
Write-TestHeader "Script 模式测试"

# 测试 1: 简单表达式
$totalTests++
if (Invoke-CodeTest -TestName "简单表达式" -Code "1 + 2" -ExpectedResult 3) { $passedTests++ }

# 测试 2: Console.WriteLine
$totalTests++
if (Invoke-CodeTest -TestName "Console.WriteLine" -Code 'Console.WriteLine("Hello Script!");' -ExpectedOutput "Hello Script!") { $passedTests++ }

# 测试 3: 多行语句带返回值
$totalTests++
$scriptCode = @'
Console.WriteLine("Calculating...");
int a = 10;
int b = 20;
int result = a + b;
Console.WriteLine($"Result: {result}");
result
'@
if (Invoke-CodeTest -TestName "多行语句带返回值" -Code $scriptCode -ExpectedResult 30 -ExpectedOutput "Result: 30") { $passedTests++ }

# 测试 4: LINQ
$totalTests++
$linqCode = 'Enumerable.Range(1, 5).Sum()'
if (Invoke-CodeTest -TestName "LINQ 表达式" -Code $linqCode -ExpectedResult 15) { $passedTests++ }

# ============================================
# Program 模式测试
# ============================================
Write-TestHeader "Program 模式测试"

# 测试 5: void Main()
$totalTests++
$voidMainCode = @'
public class Program
{
    public static void Main()
    {
        Console.WriteLine("Hello from void Main!");
    }
}
'@
if (Invoke-CodeTest -TestName "void Main()" -Code $voidMainCode -ExpectedOutput "Hello from void Main!") { $passedTests++ }

# 测试 6: int Main()
$totalTests++
$intMainCode = @'
public class Program
{
    public static int Main()
    {
        Console.WriteLine("Hello from int Main!");
        return 42;
    }
}
'@
if (Invoke-CodeTest -TestName "int Main()" -Code $intMainCode -ExpectedResult 42 -ExpectedOutput "Hello from int Main!") { $passedTests++ }

# 测试 7: Main(string[] args)
$totalTests++
$argsMainCode = @'
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine($"Args count: {args.Length}");
    }
}
'@
if (Invoke-CodeTest -TestName "Main(string[] args)" -Code $argsMainCode -ExpectedOutput "Args count: 0") { $passedTests++ }

# 测试 8: async Task Main()
$totalTests++
$asyncMainCode = @'
public class Program
{
    public static async Task Main()
    {
        await Task.Delay(50);
        Console.WriteLine("Hello from async Main!");
    }
}
'@
if (Invoke-CodeTest -TestName "async Task Main()" -Code $asyncMainCode -ExpectedOutput "Hello from async Main!") { $passedTests++ }

# 测试 9: async Task<int> Main()
$totalTests++
$asyncIntMainCode = @'
public class Program
{
    public static async Task<int> Main()
    {
        await Task.Delay(50);
        Console.WriteLine("Hello from async Task<int> Main!");
        return 123;
    }
}
'@
if (Invoke-CodeTest -TestName "async Task<int> Main()" -Code $asyncIntMainCode -ExpectedResult 123 -ExpectedOutput "Hello from async Task<int> Main!") { $passedTests++ }

# 测试 10: Program 带 using 语句
$totalTests++
$withUsingsCode = @'
using System;
using System.Linq;

public class Program
{
    public static void Main()
    {
        var sum = Enumerable.Range(1, 10).Sum();
        Console.WriteLine($"Sum 1-10: {sum}");
    }
}
'@
if (Invoke-CodeTest -TestName "Program 带 using 语句" -Code $withUsingsCode -ExpectedOutput "Sum 1-10: 55") { $passedTests++ }

# 测试 11: Program 模式 HttpClient 测试 (验证程序集引用修复)
$totalTests++
$httpClientProgramCode = @'
using System;
using System.Net.Http;
using System.Threading.Tasks;

public static class Program
{
    public static async Task Main()
    {
        try
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync("https://www.baidu.com/");
                Console.WriteLine("Status: " + response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }    
}
'@
if (Invoke-CodeTest -TestName "Program 模式 HttpClient 测试" -Code $httpClientProgramCode -ExpectedOutput "Status: OK") { $passedTests++ }

# ============================================
# 测试结果汇总
# ============================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " 测试结果汇总" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$resultColor = if ($passedTests -eq $totalTests) { "Green" } else { "Yellow" }
Write-Host "`n通过: $passedTests / $totalTests" -ForegroundColor $resultColor

if ($passedTests -eq $totalTests) {
    Write-Host "`n🎉 所有测试通过！" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  部分测试失败" -ForegroundColor Yellow
    exit 1
}
