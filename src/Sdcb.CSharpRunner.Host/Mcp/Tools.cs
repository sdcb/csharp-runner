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
Run C# code in a sandboxed environment, default timeout: 30000(ms)

The `code` parameter accepts a **string** that contains the C# code you wish to execute. This is like writing code in a special C# interactive environment (REPL), rather than creating a complete console application.

-----

## Code Specification

### 1. Basic Format

You can write either **expressions** or **statements**.

  * **Expression:** A piece of code that can be evaluated to a value. The result of the last expression will be the return value of the entire code.
      * Example: `"1 + 2"`, returns `3`.
      * Example: `"Math.Sqrt(16)"`, returns `4`.
  * **Statements:** A series of operational instructions, which can include variable definitions, loops, conditional judgments, etc. You can use the `return` keyword to specify the return value.
      * Example: `"int a = 10; int b = 20; return a + b;"`, returns `30`.
      * If there is no `return` statement and the last line is an expression, the value of that expression will be returned. For example: `"var x = 5; x * 10"`, returns `50`.

### 2. Forbidden Format ❌

You **cannot** provide a complete program structure that includes a `Main` method. Because the code is not compiled and run as a standalone program, it does not have a `Main` function as an entry point.

The following format is **incorrect** and will not run:

```csharp
// Incorrect Example
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}
```

If you want to achieve the same effect as the code above, you should write it directly:

```csharp
// Correct Example
Console.WriteLine("Hello, World!");
```

### 3. Special Note on `using` Declarations 💡

Please note, the **`using` declaration** syntax introduced in C# 8.0 (e.g., `using var client = new HttpClient();`) is **not directly supported at the top level** of the script.

This is because the script environment requires top-level statements to be complete statement blocks. A `using` declaration is considered a local variable declaration and requires a clear scope (a code block `{...}`).

  * **Incorrect Example (will cause a compilation error):**

    ```csharp
    // This will fail
    using var client = new HttpClient();
    return await client.GetStringAsync("https://www.google.com");
    ```

  * **Correct Solutions:**

    1.  **Use the traditional `using` statement:** This format is always valid.

        ```csharp
        // Correct way 1
        using (var client = new HttpClient())
        {
            return await client.GetStringAsync("https://www.google.com");
        }
        ```

    2.  **Wrap it in a block:** Place the `using` declaration inside a pair of curly braces `{}` to create a scope. The object will be disposed at the end of the block.

        ```csharp
        // Correct way 2
        {
            using var client = new HttpClient();
            return await client.GetStringAsync("https://www.google.com");
        } // client is disposed here
        ```

### 4. Code Examples

  * **Simple Calculation:**
    ```json
    { "code": "3.14 * 10 * 10" }
    ```
  * **Using a Loop and Console Output:**
    ```json
    {
      "code": "for(int i = 0; i < 5; i++) { Console.WriteLine($\"The current number is: {i}\"); } return \"Loop finished\";"
    }
    ```
    This code will print 5 lines of text and finally return the string "Loop finished".
  * **Using a LINQ Query:**
    ```json
    {
      "code": "var numbers = new int[] { 1, 2, 3, 4, 5, 6 }; var evenNumbers = numbers.Where(n => n % 2 == 0).ToList(); return evenNumbers;"
    }
    ```
    This code will return a list containing `{ 2, 4, 6 }`.

### 5. Output and Return

  * **Console Output (`Console.WriteLine`)**: You can use `Console.WriteLine` or `Console.Error.WriteLine` to print information. This output is captured in real-time and returned.
  * **Return Value (`return`)**: The return value of the code is the result of the last evaluated expression or the value explicitly specified by a `return` statement.

-----

## Environment Preset

When your code is executed, the system has already automatically referenced common assemblies and imported namespaces. You do not need to write statements like `using System;` yourself.

### Pre-Referenced Assemblies

Your code can directly use the functionalities from the following core libraries:

  * .NET Core Libraries (`System.Private.CoreLib.dll`, `System.Runtime.dll`)
  * `System.Linq.dll` (LINQ features)
  * `System.Console.dll` (Console features)
  * `System.Threading.Thread.dll` (Multithreading features)
  * `System.Xml.XDocument.dll` (XML LINQ features)
  * `System.Net.Http.dll` (`HttpClient` features)
  * `System.Text.Json.dll` (JSON serialization features)
  * `System.Security.Cryptography.Algorithms.dll` (Cryptography algorithms)
  * `System.Runtime.Numerics.dll` (`BigInteger`, etc.)

### Pre-imported Namespaces

The following namespaces are already automatically imported, and you can use their classes and methods directly and do not need to write `using` statements in your code:

  * `System`
  * `System.Collections`
  * `System.Collections.Concurrent`
  * `System.Collections.Generic`
  * `System.Diagnostics`
  * `System.IO`
  * `System.Linq`
  * `System.Net`
  * `System.Net.Http`
  * `System.Numerics`
  * `System.Reflection`
  * `System.Security`
  * `System.Security.Cryptography`
  * `System.Text`
  * `System.Text.Json`
  * `System.Text.RegularExpressions`
  * `System.Threading`
  * `System.Threading.Tasks`
  * `System.Xml`
  * `System.Xml.Linq`
  * `System.Xml.XPath`
""")]
    public async Task<FinalResponse> RunCode(string code, IProgress<ProgressNotificationValue> progress, int timeout = 30_000)
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
