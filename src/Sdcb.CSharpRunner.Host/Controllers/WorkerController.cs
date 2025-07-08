using Microsoft.AspNetCore.Mvc;

namespace Sdcb.CSharpRunner.Host.Controllers;

[Route("api/[controller]")]
public class WorkerController(RoundRobinPool<Worker> db, IHttpClientFactory http) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterWorkerRequest worker)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Console.WriteLine($"Registering worker: {worker.WorkerUrl}, MaxRuns: {worker.MaxRuns}");
        string? errorMessage = await worker.Validate(http);
        if (errorMessage != null)
        {
            Console.WriteLine(errorMessage);
            return BadRequest(errorMessage);
        }

        db.Add(worker.CreateWorker());
        Console.WriteLine("Worker registration successful.");
        return Ok(new { message = "Worker registered successfully." });
    }
}
