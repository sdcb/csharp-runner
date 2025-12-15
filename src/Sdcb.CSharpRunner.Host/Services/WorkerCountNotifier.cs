using Microsoft.AspNetCore.SignalR;
using Sdcb.CSharpRunner.Host.Hubs;

namespace Sdcb.CSharpRunner.Host.Services;

public class WorkerCountNotifier : BackgroundService
{
    private readonly RoundRobinPool<Worker> _pool;
    private readonly IHubContext<WorkerHub> _hubContext;

    public WorkerCountNotifier(RoundRobinPool<Worker> pool, IHubContext<WorkerHub> hubContext)
    {
        _pool = pool;
        _hubContext = hubContext;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _pool.CountChanged += OnCountChanged;
        
        // Keep the service alive
        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.SetResult());
        return tcs.Task;
    }

    private void OnCountChanged(int count)
    {
        _hubContext.Clients.All.SendAsync("UpdateWorkerCount", count);
    }

    public override void Dispose()
    {
        _pool.CountChanged -= OnCountChanged;
        base.Dispose();
    }
}
