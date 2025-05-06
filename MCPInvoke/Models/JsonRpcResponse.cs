using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPInvoke.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 Response object.
/// </summary>
public class JsonRpcResponse
{
    /// <summary>
    /// Gets the JSON-RPC version string. Must be "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpcVersion { get; } = "2.0";

    /// <summary>
    /// Gets or sets the result of the method invocation. This member is REQUIRED on success.
    /// This member MUST NOT exist if there was an error invoking the method.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets the error object if an error occurred. This member is REQUIRED on error.
    /// This member MUST NOT exist if there was no error triggered by the method invocation.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    /// <summary>
    /// Gets or sets the identifier established by the client, which MUST match the value of the id member in the Request Object.
    /// If there was an error in detecting the id in the Request object (e.g. Parse error/Invalid Request), it MUST be Null.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; } // Should match the request ID

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcResponse"/> class.
    /// </summary>
    /// <param name="id">The identifier which should match the request ID. Can be null for notifications or if the request ID could not be determined.</param>
    /// <param name="result">The result of the successful method invocation. Should be null if an error occurred.</param>
    /// <param name="error">The error object if an error occurred. Should be null if the method invocation was successful.</param>
    /// <exception cref="ArgumentException">Thrown if both result and error are non-null.</exception>
    public JsonRpcResponse(JsonElement? id, object? result = null, JsonRpcError? error = null)
    {
        Id = id;
        Result = result;
        Error = error;

        if (result != null && error != null)
        {
            throw new ArgumentException("Both Result and Error cannot be set in a JSON-RPC response.");
        }
    }
}

/// <summary>
/// Represents a JSON-RPC 2.0 Error object.
/// </summary>
public class JsonRpcError
{
    /// <summary>
    /// Gets or sets a number that indicates the error type that occurred.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets a string providing a short description of the error.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a primitive or structured value that contains additional information about the error.
    /// This may be omitted.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}
