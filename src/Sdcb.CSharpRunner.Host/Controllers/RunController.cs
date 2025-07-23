using Microsoft.AspNetCore.Mvc;
using Sdcb.CSharpRunner.Shared;
using System.Text.Json;

namespace Sdcb.CSharpRunner.Host.Controllers;

[Route("api/[controller]")]
public class RunController(IHttpClientFactory http) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Run([FromBody] RunCodeRequest request, [FromServices] RoundRobinPool<Worker> db, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        using RunLease<Worker> worker = await db.AcquireLeaseAsync(cancellationToken);
        try
        {
            bool started = false;
            await foreach (SseResponse buffer in worker.Value.RunAsJson(http, request, cancellationToken))
            {
                if (!started)
                {
                    Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                    Response.Headers.CacheControl = "no-cache";
                    started = true;
                }

                await Response.Body.WriteAsync("data: "u8.ToArray(), cancellationToken);
                await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(buffer, AppJsonContext.Default.SseResponse), cancellationToken);
                await Response.Body.WriteAsync("\n\n"u8.ToArray(), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            await Response.CompleteAsync();
        }
        catch (InvalidOperationException e)
        {
            return StatusCode(e.HResult, e.Message);
        }

        return Empty;
    }
}
