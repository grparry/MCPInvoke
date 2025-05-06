using System.Collections.Generic;

// This namespace might change if we later move it to a subfolder
// For now, keeping it aligned with the project's root namespace for simplicity.
namespace MCPInvoke;

/// <summary>
/// Placeholder class representing a tool definition as might be provided by MCPBuckle.
/// </summary>
public class McpToolDefinition
{
    /// <summary>
    /// The JSON-RPC method name (e.g., "Namespace.ToolName").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The assembly-qualified name of the .NET type that handles this tool.
    /// Example: "MyNamespace.MyService, MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    /// </summary>
    public string HandlerTypeAssemblyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// The C# method name on the handler type.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Describes the input parameters for the tool.
    /// </summary>
    public List<McpParameterInfo> InputSchema { get; set; } = new List<McpParameterInfo>();

    // Future considerations from MCPBuckle definition:
    /// <summary>
    /// Optional. A human-readable description of the tool itself.
    /// </summary>
    public string? Description { get; set; }
    // public McpSchema? OutputSchema { get; set; } // If MCPInvoke needs to validate/document output
    // public bool IsStatic { get; set; } // Could be inferred from MethodInfo later, or specified here
}
