using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Worker;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(SseResponse))]
[JsonSerializable(typeof(RunCodeRequest))]
[JsonSerializable(typeof(RegisterWorkerRequest))]
internal partial class AppJsonContext : JsonSerializerContext
{
}