using Microsoft.AspNetCore.Mvc;

namespace Sdcb.CSharpRunner.Host.Controllers;

[Route("api/[controller]")]
public class WorkerController(RoundRobinPool<Worker> db, IHttpClientFactory http) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterWorkerRequest worker, [FromServices] ILogger<WorkerController> logger)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        logger.LogInformation("Registering worker: {WorkerUrl}, MaxRuns: {MaxRuns}", worker.WorkerUrl, worker.MaxRuns);
        string? errorMessage = await worker.Validate(http);
        if (errorMessage != null)
        {
            logger.LogError(errorMessage);
            return BadRequest(errorMessage);
        }

        db.Add(worker.CreateWorker());
        logger.LogInformation("Worker registration successful.");
        return Ok(new { message = "Worker registered successfully." });
    }

    [HttpGet("count")]
    public int GetWorkerCount()
    {
        return db.Count;
    }
}
