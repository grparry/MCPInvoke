using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPInvoke.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 Request object.
/// </summary>
public class JsonRpcRequest
{
    /// <summary>
    /// Gets or sets the JSON-RPC version string. Must be "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpcVersion { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the name of the method to be invoked.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the structured value that holds the parameter values to be used during the invocation of the method. This can be an object or an array.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; } // Using JsonElement for flexibility, can be object or array

    /// <summary>
    /// Gets or sets an identifier established by the client. Can be a string, a number, or null.
    /// If null, the request is assumed to be a notification.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; } // Can be string, number, or null
}
