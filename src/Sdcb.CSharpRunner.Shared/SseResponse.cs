using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Shared;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StdoutSseResponse), "stdout")]
[JsonDerivedType(typeof(StdErrSseResponse), "stderr")]
[JsonDerivedType(typeof(ResultSseResponse), "result")]
[JsonDerivedType(typeof(CompilerErrorSseResponse), "compilerError")]
[JsonDerivedType(typeof(ErrorSseResponse), "error")]
[JsonDerivedType(typeof(EndSseResponse), "end")]
public record SseResponse;

public record StdoutSseResponse : SseResponse
{
    [JsonPropertyName("stdOutput")]
    public required string StdOutput { get; init; }
}

public record StdErrSseResponse : SseResponse
{
    [JsonPropertyName("stdError")]
    public required string StdError { get; init; }
}

public record ResultSseResponse : SseResponse
{
    [JsonPropertyName("result")]
    public required object? Result { get; init; }
}

public record CompilerErrorSseResponse : SseResponse
{
    [JsonPropertyName("compilationError")]
    public required string CompilerError { get; init; }
}

public record ErrorSseResponse : SseResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}

/// <summary>
/// Represents the response for ending a Server-Sent Events (SSE) operation,  containing the standard output, standard
/// error, result, and any associated error information.
/// </summary>
/// <remarks>This response is typically used to convey the outcome of an SSE operation,  including any output or
/// errors generated during the operation.</remarks>
public record EndSseResponse : SseResponse
{
    [JsonPropertyName("stdOutput"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StdOutput { get; init; }

    [JsonPropertyName("stdError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StdError { get; init; }

    [JsonPropertyName("result"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    [JsonPropertyName("compilerError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompilerError { get; init; }

    [JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("elapsed")]
    public required long Elapsed { get; init; }

    public FinalResponse ToFinalResponse()
    {
        return new FinalResponse
        {
            StdOutput = StdOutput,
            StdError = StdError,
            Result = Result,
            CompilerError = CompilerError,
            Error = Error,
            Elapsed = Elapsed
        };
    }
}

/// <summary>
/// Represents the response for ending a Server-Sent Events (SSE) operation,  containing the standard output, standard
/// error, result, and any associated error information.
/// </summary>
/// <remarks>This response is typically used to convey the outcome of an SSE operation,  including any output or
/// errors generated during the operation.</remarks>
public record FinalResponse
{
    [JsonPropertyName("stdOutput"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StdOutput { get; init; }

    [JsonPropertyName("stdError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StdError { get; init; }

    [JsonPropertyName("result"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    [JsonPropertyName("compilerError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompilerError { get; init; }

    [JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("elapsed")]
    public required long Elapsed { get; init; }
}