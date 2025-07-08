using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Worker;

public record RunCodeRequest
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }
}