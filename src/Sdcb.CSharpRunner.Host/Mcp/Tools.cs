using Sdcb.CSharpRunner.Host.Mcp.Details;
using Sdcb.CSharpRunner.Shared;
using System.ComponentModel;
using System.Text.Json;

namespace Sdcb.CSharpRunner.Host.Mcp;

public class Tools(RoundRobinPool<Worker> db, IHttpClientFactory http)
{
    [Description("Run C# code in a sandboxed environment")]
    public async Task<EndSseResponse> RunCode(string code, IProgress<ProgressNotificationValue> progress, int timeout = 30_000)
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
                    Message = JsonSerializer.Serialize(buffer, AppJsonContext.Default.SseResponse)
                });
            }
        }

        return endResponse;
    }
}
