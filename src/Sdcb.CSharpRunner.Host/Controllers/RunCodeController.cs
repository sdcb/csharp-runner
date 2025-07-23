using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Sdcb.CSharpRunner.Host.Controllers;

[Route("api/[controller]")]
public class RunCodeController(IHttpClientFactory http) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RunCode([FromBody] RunCodeRequest request, [FromServices] RoundRobinPool<Worker> db)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        using RunLease<Worker> worker = await db.AcquireLeaseAsync();
        using HttpResponseMessage resp = await worker.Value.Run(http, request);
        if (!resp.IsSuccessStatusCode)
        {
            return StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        var feature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
        await using Stream stream = await resp.Content.ReadAsStreamAsync();
        byte[] buffer = new byte[80 * 1024];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, HttpContext.RequestAborted);
            if (read == 0) break;

            await Response.Body.WriteAsync(buffer.AsMemory(0, read), HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        await Response.CompleteAsync();
        return Empty;
    }
}
