using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Worker;

public record RunCodeRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("timeout")] int Timeout = 30_000,
    [property: JsonPropertyName("isWarmUp")] bool IsWarmUp = false
);