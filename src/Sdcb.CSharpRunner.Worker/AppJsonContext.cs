using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Worker;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(SseResponse))]
[JsonSerializable(typeof(RunCodeRequest))]
internal partial class AppJsonContext : JsonSerializerContext
{
}