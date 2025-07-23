using Microsoft.AspNetCore.Mvc;
using Sdcb.CSharpRunner.Shared;

namespace Sdcb.CSharpRunner.Host.Controllers;

[Route("api/[controller]")]
public class RunCodeController(IHttpClientFactory http) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RunCode([FromBody] RunCodeRequest request, [FromServices] RoundRobinPool<Worker> db, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        using RunLease<Worker> worker = await db.AcquireLeaseAsync(cancellationToken);
        try
        {
            bool started = false;
            await foreach (Memory<byte> buffer in worker.Value.RunAsMemory(http, request, cancellationToken))
            {
                if (!started)
                {
                    Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                    Response.Headers.CacheControl = "no-cache";
                    started = true;
                }

                await Response.Body.WriteAsync(buffer, cancellationToken);
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
