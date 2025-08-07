// This namespace might change if we later move it to a subfolder
// For now, keeping it aligned with the project's root namespace for simplicity.
namespace MCPInvoke;

/// <summary>
/// Represents parameter information for a tool, part of its input schema.
/// Enhanced to support MCP specification compliance with complex object schemas.
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
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the parameter is required for the tool invocation.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Optional. A human-readable description of the parameter.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Additional annotations for this parameter, including type metadata
    /// that's not captured by the basic JSON Schema type system.
    /// </summary>
    /// <remarks>
    /// This can include:
    /// - "properties": Dictionary of object properties for complex objects
    /// - "required": List of required property names for objects
    /// - "items": Schema for array item types
    /// - "enum": List of allowed values for enums
    /// - "source": Parameter source ("route", "body", "query", "header")
    /// - "format": Format specification (e.g., "date-time", "email")
    /// </remarks>
    public Dictionary<string, object>? Annotations { get; set; }

    /// <summary>
    /// MCP-compliant schema properties for object types.
    /// Contains property definitions when Type is "object".
    /// </summary>
    public Dictionary<string, McpParameterInfo>? Properties { get; set; }

    /// <summary>
    /// List of required properties for object types.
    /// Used when Type is "object" to specify which properties are mandatory.
    /// </summary>
    public List<string>? Required { get; set; }

    /// <summary>
    /// Schema definition for array item types.
    /// Used when Type is "array" to define the structure of array elements.
    /// </summary>
    public McpParameterInfo? Items { get; set; }

    /// <summary>
    /// List of allowed values for enum types.
    /// Used when the parameter has a restricted set of valid values.
    /// </summary>
    public List<object>? Enum { get; set; }

    /// <summary>
    /// Format specification for string types.
    /// Examples: "date-time", "email", "uri", "uuid"
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Default value for optional parameters.
    /// </summary>
    public object? Default { get; set; }

    /// <summary>
    /// Parameter source information for proper binding.
    /// Values: "route", "body", "query", "header", "form"
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Creates a deep copy of this parameter info.
    /// Useful for schema caching and circular reference handling.
    /// </summary>
    public McpParameterInfo Clone()
    {
        return new McpParameterInfo
        {
            Name = Name,
            Type = Type,
            IsRequired = IsRequired,
            Description = Description,
            Annotations = Annotations?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Properties = Properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
            Required = Required?.ToList(),
            Items = Items?.Clone(),
            Enum = Enum?.ToList(),
            Format = Format,
            Default = Default,
            Source = Source
        };
    }

    /// <summary>
    /// Validates that this parameter info conforms to MCP specification requirements.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(Name))
        {
            errors.Add("Parameter name is required");
        }

        if (string.IsNullOrEmpty(Type))
        {
            errors.Add("Parameter type is required");
        }
        else
        {
            var validTypes = new[] { "string", "integer", "number", "boolean", "object", "array", "null" };
            if (!validTypes.Contains(Type))
            {
                errors.Add($"Invalid parameter type '{Type}'. Must be one of: {string.Join(", ", validTypes)}");
            }

            // Type-specific validation
            if (Type == "object" && Properties == null && (Annotations?.ContainsKey("properties") != true))
            {
                errors.Add("Object parameters must have properties definition");
            }

            if (Type == "array" && Items == null && (Annotations?.ContainsKey("items") != true))
            {
                errors.Add("Array parameters must have items schema");
            }
        }

        // Validate nested properties
        if (Properties != null)
        {
            foreach (var prop in Properties.Values)
            {
                errors.AddRange(prop.Validate().Select(error => $"Property '{prop.Name}': {error}"));
            }
        }

        // Validate items schema
        if (Items != null)
        {
            errors.AddRange(Items.Validate().Select(error => $"Items schema: {error}"));
        }

        return errors;
    }
}
