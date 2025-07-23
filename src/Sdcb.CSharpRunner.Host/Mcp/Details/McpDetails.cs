using System.Text.Json.Serialization;

namespace Sdcb.CSharpRunner.Host.Mcp.Details;

// --- JSON-RPC Base Structures ---
public record JsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] object? Params,
    [property: JsonPropertyName("id")] int? Id
);

public record JsonRpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] object? Error,
    [property: JsonPropertyName("id")] int? Id
);

public record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);

// --- MCP Specific Payloads ---

// For initialize method
public record InitializeParams(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("clientInfo")] ClientInfo ClientInfo
);
public record ClientInfo([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("version")] string Version);

public record InitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] ServerCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] ClientInfo ServerInfo
);
public record ServerCapabilities([property: JsonPropertyName("tools")] object Tools);


// For tools/call method
public record ToolCallParams(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] Dictionary<string, object?> Arguments,
    [property: JsonPropertyName("_meta")] ToolCallMeta? Meta
);
public record ToolCallMeta([property: JsonPropertyName("progressToken")] string ProgressToken);

// For tool call results
public record ToolCallResult(
    [property: JsonPropertyName("content")] List<ContentItem> Content,
    [property: JsonPropertyName("isError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsError = false,
    [property: JsonPropertyName("structuredContent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] object? StructuredContent = null
);
public record ContentItem([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string Text);

// For tools/list results
public record ToolListResult(
    [property: JsonPropertyName("tools")] List<ToolDefinition> Tools
);

public record ToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema
);

// For progress notifications
public record ProgressNotification(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] ProgressParams Params
);
public record ProgressParams(
    [property: JsonPropertyName("progressToken")] string ProgressToken,
    [property: JsonPropertyName("progress")] int Progress,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("message")] string Message
);

// This class is for the IProgress<T> interface in our Tools methods
public class ProgressNotificationValue
{
    public int Progress { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = string.Empty;
}
