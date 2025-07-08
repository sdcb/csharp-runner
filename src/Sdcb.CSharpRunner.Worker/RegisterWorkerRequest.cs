using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Worker;

public record RegisterWorkerRequest
{
    [JsonPropertyName("workerUrl")]
    public required string WorkerUrl { get; init; }

    [JsonPropertyName("maxRuns")]
    public required int MaxRuns { get; init; }
}
