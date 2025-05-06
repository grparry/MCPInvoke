using System.Reflection;
using System.Text.Json;

namespace MCPInvoke.Models;

/// <summary>
/// Represents a tool that has been registered and is available for execution.
/// </summary>
public class RegisteredTool
{
    /// <summary>
    /// The unique name of the tool (e.g., "WeatherService.GetForecast").
    /// This will be matched against the 'method' in the JSON-RPC request.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// The MethodInfo object representing the C# method to be invoked.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// The type that declares the method. For instance methods, an instance of this type
    /// will need to be resolved from the service provider.
    /// </summary>
    public Type DeclaringType { get; }

    /// <summary>
    /// Indicates whether the method is static. If true, no instance needs to be resolved.
    /// </summary>
    public bool IsStatic => Method.IsStatic;

    /// <summary>
    /// A pre-parsed representation of the MCP tool schema's input parameters.
    /// This helps in validating and deserializing the 'params' from the JSON-RPC request.
    /// Key is parameter name, Value is a placeholder for parameter type/schema info.
    /// TODO: This will need to align with McpBuckle.Models.McpToolSchemaProperty or similar.
    /// </summary>
    public Dictionary<string, McpToolSchemaPropertyPlaceholder> InputParameters { get; }

    /// <summary>
    /// Placeholder for MCP Tool Schema Property details.
    /// This should eventually be replaced or aligned with the actual McpToolSchemaProperty from MCPBuckle.
    /// </summary>
    public record McpToolSchemaPropertyPlaceholder(Type ClrType, bool IsRequired, JsonElement? DefaultValue);

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisteredTool"/> class.
    /// </summary>
    /// <param name="toolName">The unique name of the tool.</param>
    /// <param name="method">The <see cref="MethodInfo"/> for the tool's execution.</param>
    /// <param name="declaringType">The <see cref="Type"/> that declares the method.</param>
    /// <param name="inputParameters">A dictionary representing the tool's input schema parameters.</param>
    public RegisteredTool(
        string toolName,
        MethodInfo method,
        Type declaringType,
        Dictionary<string, McpToolSchemaPropertyPlaceholder> inputParameters)
    {
        ToolName = toolName;
        Method = method;
        DeclaringType = declaringType;
        InputParameters = inputParameters;
    }
}
