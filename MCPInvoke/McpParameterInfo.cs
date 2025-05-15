// This namespace might change if we later move it to a subfolder
// For now, keeping it aligned with the project's root namespace for simplicity.
namespace MCPInvoke;

/// <summary>
/// Placeholder class representing parameter information for a tool, part of its input schema.
/// </summary>
public class McpParameterInfo
{
    /// <summary>
    /// The name of the parameter (e.g., "count", "userName").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The JSON schema type of the parameter (e.g., "integer", "string", "boolean", "number", "array", "object").
    /// </summary>
    public string Type { get; set; } = string.Empty; // Consider using an enum or constants for standard JSON schema types

    /// <summary>
    /// Indicates if the parameter is required for the tool invocation.
    /// </summary>
    public bool IsRequired { get; set; }

    // Future considerations from MCPBuckle schema:
    /// <summary>
    /// Optional. A human-readable description of the parameter.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Additional annotations for this parameter, including type metadata
    /// that's not captured by the basic JSON Schema type system.
    /// </summary>
    /// <remarks>
    /// This can include information like whether a parameter is an enum,
    /// whether it uses string representation (JsonStringEnumConverter),
    /// and other parameter-specific metadata.
    /// </remarks>
    public Dictionary<string, object>? Annotations { get; set; }
    
    // public object? DefaultValue { get; set; }
    // public McpSchema? SchemaDetails { get; set; } // For more complex types like objects or arrays with specific item schemas
}
