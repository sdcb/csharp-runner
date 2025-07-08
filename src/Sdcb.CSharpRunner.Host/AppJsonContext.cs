using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Host;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(RunCodeRequest))]
public partial class AppJsonContext : JsonSerializerContext
{
}
