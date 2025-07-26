using Sdcb.CSharpRunner.Shared;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Host;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(RunCodeRequest))]
[JsonSerializable(typeof(SseResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(FinalResponse))]
public partial class AppJsonContext : JsonSerializerContext
{
}
