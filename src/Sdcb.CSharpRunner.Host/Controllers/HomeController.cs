using Microsoft.AspNetCore.Mvc;

namespace Sdcb.CSharpRunner.Host.Controllers;

public class HomeController : ControllerBase
{
    [HttpGet("")]
    public IActionResult Index([FromServices] RoundRobinPool<Worker> db)
    {
        string html = $$"""
            <!doctype html><html lang="zh-CN">
            <head><meta charset="utf-8"><title>C# Runner is Ready</title></head>
            <body style="font-family:sans-serif">
              <h1>Sdcb.CSharpRunner Host is READY ✅</h1>
              <p>POST <code>/api/runcode</code> send <code>{"code":"3+4"}</code> to run it.</p>
              <p>Registered Worker Count: <b>{{ db.Count }}</b></p>
              <p>Please refer to <a href="https://github.com/sdcb/csharp-runner">https://github.com/sdcb/csharp-runner</a> for more information.</p>
            </body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }
}
