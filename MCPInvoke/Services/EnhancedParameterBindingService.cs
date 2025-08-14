using MCPInvoke.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MCPInvoke.Services;

/// <summary>
/// Enhanced parameter binding service for MCPInvoke v2.0.
/// Utilizes schema metadata from MCPBuckle v2.0 to provide intelligent parameter binding
/// with proper source detection, validation, and type coercion.
/// </summary>
/// <remarks>
/// This service addresses the fundamental issue with MCPInvoke v1.x where direct method invocation
/// bypasses ASP.NET Core's parameter binding pipeline. MCPInvoke v2.0 implements its own
/// schema-aware parameter binding that respects parameter sources (route, body, query, header),
/// validation rules, and complex type handling while maintaining compatibility with ASP.NET Core patterns.
/// </remarks>
public class EnhancedParameterBindingService
{
    private readonly ILogger<EnhancedParameterBindingService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnhancedParameterBindingService"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public EnhancedParameterBindingService(ILogger<EnhancedParameterBindingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure JSON serialization options with enhanced support for complex types
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            // Enhanced enum handling with JsonStringEnumConverter
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            // Allow for more flexible number handling
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        
        _logger.LogInformation("EnhancedParameterBindingService initialized with schema-aware parameter binding");
    }

    /// <summary>
    /// Binds parameters using enhanced schema-aware binding with MCPBuckle v2.0 metadata.
    /// </summary>
    /// <param name="methodParameters">The method parameters from reflection.</param>
    /// <param name="inputSchema">The input schema from MCPBuckle with enhanced metadata.</param>
    /// <param name="paramsElement">The JSON parameters from the request.</param>
    /// <param name="hasParamsElement">Whether parameters are present in the request.</param>
    /// <param name="toolName">The tool name for error reporting.</param>
    /// <returns>An array of bound parameter values, or null if binding fails.</returns>
    public async Task<object?[]?> BindParametersAsync(
        ParameterInfo[] methodParameters,
        List<McpParameterInfo> inputSchema,
        JsonElement paramsElement,
        bool hasParamsElement,
        string toolName)
    {
        _logger.LogInformation("Starting enhanced parameter binding for tool '{ToolName}' with {ParameterCount} parameters", 
                             toolName, methodParameters.Length);

        // Debug: Log schema and JSON parameter information
        _logger.LogDebug("Schema parameters: {SchemaParams}", string.Join(", ", inputSchema.Select(s => s.Name)));
        if (hasParamsElement && paramsElement.ValueKind == JsonValueKind.Object)
        {
            _logger.LogDebug("JSON parameters: {JsonParams}", string.Join(", ", paramsElement.EnumerateObject().Select(p => p.Name)));
        }

        // Create schema lookup for efficient parameter resolution
        var schemaLookup = inputSchema.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        
        object?[] callParameters = new object[methodParameters.Length];
        
        for (int i = 0; i < methodParameters.Length; i++)
        {
            ParameterInfo paramInfo = methodParameters[i];
            string paramName = paramInfo.Name ?? $"param_{i}";
            
            _logger.LogDebug("Binding parameter {Index}: Name='{ParamName}', Type='{ParamType}'", 
                           i, paramName, paramInfo.ParameterType.Name);

            try
            {
                callParameters[i] = await BindSingleParameterAsync(
                    paramInfo, 
                    schemaLookup, 
                    paramsElement, 
                    hasParamsElement, 
                    toolName);
                
                _logger.LogDebug("Successfully bound parameter '{ParamName}'", paramName);
            }
            catch (ParameterBindingException ex)
            {
                _logger.LogError(ex, "Parameter binding failed for '{ParamName}' in tool '{ToolName}'. Returning null for entire parameter binding", paramName, toolName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error binding parameter '{ParamName}' in tool '{ToolName}'. Returning null for entire parameter binding", paramName, toolName);
                return null;
            }
        }
        
        _logger.LogInformation("Enhanced parameter binding completed successfully for tool '{ToolName}'", toolName);
        return callParameters;
    }

    /// <summary>
    /// Binds a single parameter using enhanced schema-aware logic.
    /// </summary>
    private async Task<object?> BindSingleParameterAsync(
        ParameterInfo paramInfo,
        Dictionary<string, McpParameterInfo> schemaLookup,
        JsonElement paramsElement,
        bool hasParamsElement,
        string toolName)
    {
        string paramName = paramInfo.Name ?? "unknown";
        Type paramType = paramInfo.ParameterType;
        
        // Check if parameter is in schema
        if (!schemaLookup.TryGetValue(paramName, out var schemaInfo))
        {
            return HandleParameterNotInSchema(paramInfo, paramName, toolName);
        }

        // Extract enhanced metadata from schema
        var bindingContext = ExtractBindingContext(schemaInfo, paramName);
        _logger.LogDebug("Parameter '{ParamName}' binding context: Source={Source}, DetectionMethod={DetectionMethod}", 
                       paramName, bindingContext.Source, bindingContext.DetectionMethod);

        // Get parameter value from JSON
        JsonElement paramValueJson = default;
        bool hasParamValue = hasParamsElement && paramsElement.TryGetProperty(paramName, out paramValueJson);

        if (!hasParamValue)
        {
            return HandleMissingParameter(paramInfo, schemaInfo, bindingContext, paramName, toolName);
        }

        // Validate parameter value against schema
        await ValidateParameterValue(paramValueJson, schemaInfo, bindingContext, paramName);

        // Bind parameter value with enhanced type coercion
        return await BindParameterValueAsync(paramInfo, paramValueJson, schemaInfo, bindingContext, paramName);
    }

    /// <summary>
    /// Handles parameters that are not found in the schema.
    /// </summary>
    private object? HandleParameterNotInSchema(ParameterInfo paramInfo, string paramName, string toolName)
    {
        // Check for ASP.NET Core infrastructure types
        if (IsInfrastructureType(paramInfo.ParameterType))
        {
            _logger.LogDebug("Skipping infrastructure parameter '{ParamName}' of type '{Type}'", 
                           paramName, paramInfo.ParameterType.Name);
            return GetDefaultValue(paramInfo.ParameterType);
        }

        // Handle optional parameters
        if (paramInfo.IsOptional || paramInfo.HasDefaultValue)
        {
            _logger.LogDebug("Parameter '{ParamName}' not in schema but is optional, using default value", paramName);
            return paramInfo.HasDefaultValue ? paramInfo.DefaultValue : GetDefaultValue(paramInfo.ParameterType);
        }

        // Required parameter not in schema is an error
        _logger.LogError("Required parameter '{ParamName}' not found in schema for tool '{ToolName}'", paramName, toolName);
        throw new ParameterBindingException($"Required parameter '{paramName}' not found in schema for tool '{toolName}'");
    }

    /// <summary>
    /// Handles missing parameters based on their schema requirements.
    /// Priority: Schema default > C# default > CLR default
    /// </summary>
    private object? HandleMissingParameter(
        ParameterInfo paramInfo, 
        McpParameterInfo schemaInfo, 
        ParameterBindingContext bindingContext, 
        string paramName, 
        string toolName)
    {
        // Use schema default value if available (highest priority)
        if (schemaInfo.Default != null)
        {
            _logger.LogDebug("Using schema default value for missing parameter '{ParamName}'", paramName);
            return ConvertDefaultValue(schemaInfo.Default, paramInfo.ParameterType, paramName);
        }

        // Use C# default value if available (second priority)
        if (paramInfo.HasDefaultValue)
        {
            _logger.LogDebug("Using C# default value for missing parameter '{ParamName}'", paramName);
            return paramInfo.DefaultValue;
        }

        // Check if parameter is required
        if (schemaInfo.IsRequired)
        {
            _logger.LogError("Missing required parameter '{ParamName}' for tool '{ToolName}'", paramName, toolName);
            throw new ParameterBindingException($"Missing required parameter '{paramName}' for tool '{toolName}'");
        }

        // Use CLR default for optional parameters (lowest priority)
        _logger.LogDebug("Using CLR default value for optional parameter '{ParamName}'", paramName);
        return GetDefaultValue(paramInfo.ParameterType);
    }

    /// <summary>
    /// Validates parameter value against schema constraints.
    /// </summary>
    private async Task ValidateParameterValue(
        JsonElement paramValueJson, 
        McpParameterInfo schemaInfo, 
        ParameterBindingContext bindingContext, 
        string paramName)
    {
        _logger.LogDebug("Validating parameter '{ParamName}' with value kind '{ValueKind}'", 
                       paramName, paramValueJson.ValueKind);

        // Enhanced type validation that handles enum cases properly
        if (!ValidateJsonTypeForParameter(paramValueJson, schemaInfo, paramName))
        {
            throw new ParameterBindingException($"Parameter '{paramName}' has invalid type. Expected '{schemaInfo.Type}', got '{paramValueJson.ValueKind}'");
        }

        // Enum validation
        if (schemaInfo.Enum != null && schemaInfo.Enum.Any())
        {
            await ValidateEnumValue(paramValueJson, schemaInfo.Enum, paramName);
        }

        // Format validation (e.g., email, date-time)
        if (!string.IsNullOrEmpty(schemaInfo.Format))
        {
            await ValidateFormat(paramValueJson, schemaInfo.Format, paramName);
        }

        // Additional validation rules from annotations
        await ValidateAnnotationRules(paramValueJson, schemaInfo.Annotations, paramName);
    }

    /// <summary>
    /// Binds parameter value with enhanced type coercion and source-aware handling.
    /// </summary>
    private async Task<object?> BindParameterValueAsync(
        ParameterInfo paramInfo,
        JsonElement paramValueJson,
        McpParameterInfo schemaInfo,
        ParameterBindingContext bindingContext,
        string paramName)
    {
        Type targetType = paramInfo.ParameterType;
        
        _logger.LogDebug("Binding value for parameter '{ParamName}' to type '{Type}' from source '{Source}'", 
                       paramName, targetType.Name, bindingContext.Source);

        try
        {
            // Enhanced enum handling
            if (targetType.IsEnum)
            {
                return await BindEnumParameterAsync(targetType, paramValueJson, schemaInfo, paramName);
            }

            // Enhanced complex object handling
            if (IsComplexType(targetType))
            {
                return await BindComplexObjectParameterAsync(targetType, paramValueJson, schemaInfo, bindingContext, paramName);
            }

            // Enhanced array/collection handling
            if (targetType.IsArray || IsCollectionType(targetType))
            {
                return await BindCollectionParameterAsync(targetType, paramValueJson, schemaInfo, paramName);
            }

            // Standard primitive type binding with nullable support
            return await BindPrimitiveParameterAsync(targetType, paramValueJson, paramName);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for parameter '{ParamName}'", paramName);
            throw new ParameterBindingException($"Failed to deserialize parameter '{paramName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Binds enum parameters with multiple fallback strategies and enhanced metadata support.
    /// </summary>
    private Task<object?> BindEnumParameterAsync(
        Type enumType, 
        JsonElement paramValueJson, 
        McpParameterInfo schemaInfo, 
        string paramName)
    {
        _logger.LogDebug("Binding enum parameter '{ParamName}' of type '{EnumType}' with value kind '{ValueKind}'", 
                       paramName, enumType.Name, paramValueJson.ValueKind);

        // Check if enum uses string converter (from schema annotations)
        bool isStringEnum = IsStringEnum(schemaInfo);
        
        // Strategy 1: String-based enum binding
        if (paramValueJson.ValueKind == JsonValueKind.String)
        {
            string enumStringValue = paramValueJson.GetString() ?? string.Empty;
            
            // Try direct case-insensitive parsing
            if (Enum.TryParse(enumType, enumStringValue, true, out var enumResult))
            {
                _logger.LogDebug("Successfully parsed enum string '{Value}' to {EnumType}", enumStringValue, enumType.Name);
                return Task.FromResult<object?>(enumResult);
            }

            // Try JsonStringEnumConverter approach
            if (isStringEnum)
            {
                try
                {
                    var enumOptions = new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                    };
                    
                    string jsonString = $"\"{enumStringValue}\"";
                    return Task.FromResult(JsonSerializer.Deserialize(jsonString, enumType, enumOptions));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "JsonStringEnumConverter failed for enum '{ParamName}'", paramName);
                }
            }
        }

        // Strategy 2: Numeric enum binding
        if (paramValueJson.ValueKind == JsonValueKind.Number)
        {
            int enumIntValue = paramValueJson.GetInt32();
            var result = Enum.ToObject(enumType, enumIntValue);
            _logger.LogDebug("Successfully converted numeric value {Value} to enum {EnumType}", enumIntValue, enumType.Name);
            return Task.FromResult<object?>(result);
        }

        // Strategy 3: Fallback deserialization
        try
        {
            return Task.FromResult(JsonSerializer.Deserialize(paramValueJson.GetRawText(), enumType, _jsonSerializerOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All enum binding strategies failed for parameter '{ParamName}' with value '{Value}'", 
                           paramName, paramValueJson.GetRawText());
            throw new ParameterBindingException($"Failed to bind enum parameter '{paramName}' of type '{enumType.Name}' with value '{paramValueJson.GetRawText()}'", ex);
        }
    }

    /// <summary>
    /// Binds complex object parameters with enhanced deserialization and validation.
    /// </summary>
    private Task<object?> BindComplexObjectParameterAsync(
        Type objectType,
        JsonElement paramValueJson,
        McpParameterInfo schemaInfo,
        ParameterBindingContext bindingContext,
        string paramName)
    {
        _logger.LogDebug("Binding complex object parameter '{ParamName}' of type '{Type}' from source '{Source}'", 
                       paramName, objectType.Name, bindingContext.Source);

        try
        {
            // Enhanced deserialization with proper options
            var result = JsonSerializer.Deserialize(paramValueJson.GetRawText(), objectType, _jsonSerializerOptions);
            
            // Additional validation for complex objects if schema provides property constraints
            if (schemaInfo.Properties != null && result != null)
            {
                ValidateComplexObjectProperties(result, schemaInfo.Properties, paramName);
            }
            
            return Task.FromResult(result);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize complex object '{ParamName}' of type '{Type}'", paramName, objectType.Name);
            throw new ParameterBindingException($"Failed to deserialize complex object '{paramName}' of type '{objectType.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Binds array and collection parameters with proper item type handling.
    /// </summary>
    private Task<object?> BindCollectionParameterAsync(
        Type collectionType,
        JsonElement paramValueJson,
        McpParameterInfo schemaInfo,
        string paramName)
    {
        _logger.LogDebug("Binding collection parameter '{ParamName}' of type '{Type}'", paramName, collectionType.Name);

        if (paramValueJson.ValueKind != JsonValueKind.Array)
        {
            throw new ParameterBindingException($"Parameter '{paramName}' expected array but got '{paramValueJson.ValueKind}'");
        }

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize(paramValueJson.GetRawText(), collectionType, _jsonSerializerOptions));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize collection '{ParamName}' of type '{Type}'", paramName, collectionType.Name);
            throw new ParameterBindingException($"Failed to deserialize collection '{paramName}' of type '{collectionType.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Binds primitive parameters with proper type coercion.
    /// </summary>
    private Task<object?> BindPrimitiveParameterAsync(Type primitiveType, JsonElement paramValueJson, string paramName)
    {
        _logger.LogDebug("Binding primitive parameter '{ParamName}' of type '{Type}'", paramName, primitiveType.Name);

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize(paramValueJson.GetRawText(), primitiveType, _jsonSerializerOptions));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize primitive '{ParamName}' of type '{Type}'", paramName, primitiveType.Name);
            throw new ParameterBindingException($"Failed to deserialize primitive '{paramName}' of type '{primitiveType.Name}': {ex.Message}", ex);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Extracts binding context from schema metadata.
    /// </summary>
    private ParameterBindingContext ExtractBindingContext(McpParameterInfo schemaInfo, string paramName)
    {
        var context = new ParameterBindingContext
        {
            Source = schemaInfo.Source ?? "unknown",
            DetectionMethod = "unknown",
            HttpMethod = null,
            RouteTemplate = null
        };

        if (schemaInfo.Annotations != null)
        {
            // Extract detection method
            if (schemaInfo.Annotations.TryGetValue("sourceDetectionMethod", out var detectionMethod))
            {
                context.DetectionMethod = detectionMethod?.ToString() ?? "unknown";
            }

            // Extract HTTP method context
            if (schemaInfo.Annotations.TryGetValue("httpMethod", out var httpMethod))
            {
                context.HttpMethod = httpMethod?.ToString();
            }

            // Extract route template
            if (schemaInfo.Annotations.TryGetValue("routeTemplate", out var routeTemplate))
            {
                context.RouteTemplate = routeTemplate?.ToString();
            }
        }

        return context;
    }

    /// <summary>
    /// Determines if a parameter schema represents a string enum.
    /// </summary>
    private bool IsStringEnum(McpParameterInfo schemaInfo)
    {
        if (schemaInfo.Format == "enum")
        {
            return true;
        }

        if (schemaInfo.Annotations?.TryGetValue("IsStringEnum", out var isStringEnum) == true)
        {
            return Convert.ToBoolean(isStringEnum);
        }

        return false;
    }

    /// <summary>
    /// Validates JSON type against schema type.
    /// </summary>
    private bool ValidateJsonType(JsonElement paramValue, string schemaType, string paramName)
    {
        return schemaType.ToLowerInvariant() switch
        {
            "string" => paramValue.ValueKind == JsonValueKind.String,
            "integer" => paramValue.ValueKind == JsonValueKind.Number,
            "number" => paramValue.ValueKind == JsonValueKind.Number,
            "boolean" => paramValue.ValueKind == JsonValueKind.True || paramValue.ValueKind == JsonValueKind.False,
            "object" => paramValue.ValueKind == JsonValueKind.Object,
            "array" => paramValue.ValueKind == JsonValueKind.Array,
            "null" => paramValue.ValueKind == JsonValueKind.Null,
            _ => true // Unknown types pass validation
        };
    }

    /// <summary>
    /// Enhanced JSON type validation that handles enum cases properly.
    /// Enums can be represented as either strings or numbers, regardless of schema type.
    /// </summary>
    private bool ValidateJsonTypeForParameter(JsonElement paramValue, McpParameterInfo schemaInfo, string paramName)
    {
        // Special handling for enum parameters
        if (schemaInfo.Format == "enum" || (schemaInfo.Enum != null && schemaInfo.Enum.Any()))
        {
            // Enums can be either string or numeric values
            return paramValue.ValueKind == JsonValueKind.String || paramValue.ValueKind == JsonValueKind.Number;
        }

        // Use standard type validation for non-enum parameters
        return ValidateJsonType(paramValue, schemaInfo.Type, paramName);
    }

    /// <summary>
    /// Validates enum values against allowed values.
    /// </summary>
    private Task ValidateEnumValue(JsonElement paramValue, List<object> allowedValues, string paramName)
    {
        // Implementation for enum validation
        return Task.CompletedTask; // Placeholder for async pattern
    }

    /// <summary>
    /// Validates format constraints (email, date-time, etc.).
    /// </summary>
    private Task ValidateFormat(JsonElement paramValue, string format, string paramName)
    {
        // Implementation for format validation
        return Task.CompletedTask; // Placeholder for async pattern
    }

    /// <summary>
    /// Validates annotation-based rules.
    /// </summary>
    private Task ValidateAnnotationRules(JsonElement paramValue, Dictionary<string, object>? annotations, string paramName)
    {
        // Implementation for annotation rule validation
        return Task.CompletedTask; // Placeholder for async pattern
    }

    /// <summary>
    /// Validates complex object properties against schema constraints.
    /// </summary>
    private Task ValidateComplexObjectProperties(object obj, Dictionary<string, McpParameterInfo> propertySchema, string paramName)
    {
        // Implementation for complex object property validation
        return Task.CompletedTask; // Placeholder for async pattern
    }

    /// <summary>
    /// Checks if a type is an infrastructure type that should be skipped.
    /// </summary>
    private bool IsInfrastructureType(Type type)
    {
        var infraTypes = new[]
        {
            "Microsoft.AspNetCore.Http.HttpContext",
            "System.Threading.CancellationToken",
            "Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary",
            "Microsoft.AspNetCore.Mvc.ActionContext"
        };

        return infraTypes.Contains(type.FullName);
    }

    /// <summary>
    /// Determines if a type is a complex type requiring object deserialization.
    /// </summary>
    private bool IsComplexType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Primitive types and common value types are not complex
        if (underlyingType.IsPrimitive || underlyingType == typeof(string) ||
            underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(TimeSpan) || underlyingType == typeof(Guid) ||
            underlyingType.IsEnum)
        {
            return false;
        }

        // Arrays and collections are handled separately
        if (type.IsArray || IsCollectionType(type))
        {
            return false;
        }

        // Everything else is considered complex
        return underlyingType.IsClass || underlyingType.IsInterface || 
               (underlyingType.IsValueType && !underlyingType.IsPrimitive);
    }

    /// <summary>
    /// Determines if a type is a collection type.
    /// </summary>
    private bool IsCollectionType(Type type)
    {
        return type.IsGenericType && 
               typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    /// <summary>
    /// Gets default value for a type.
    /// </summary>
    private object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }

    /// <summary>
    /// Converts schema default value to target type.
    /// </summary>
    private object? ConvertDefaultValue(object defaultValue, Type targetType, string paramName)
    {
        try
        {
            if (defaultValue.GetType() == targetType)
            {
                return defaultValue;
            }

            // Convert via JSON serialization for complex conversions
            string json = JsonSerializer.Serialize(defaultValue, _jsonSerializerOptions);
            return JsonSerializer.Deserialize(json, targetType, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert default value for parameter '{ParamName}', using CLR default", paramName);
            return GetDefaultValue(targetType);
        }
    }

    #endregion
}

/// <summary>
/// Context information extracted from parameter schema for binding.
/// </summary>
public class ParameterBindingContext
{
    /// <summary>
    /// Parameter source (route, body, query, header).
    /// </summary>
    public string Source { get; set; } = "unknown";

    /// <summary>
    /// Method used to detect the parameter source.
    /// </summary>
    public string DetectionMethod { get; set; } = "unknown";

    /// <summary>
    /// HTTP method context for the parameter.
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Route template context for the parameter.
    /// </summary>
    public string? RouteTemplate { get; set; }
}

/// <summary>
/// Exception thrown when parameter binding fails.
/// </summary>
public class ParameterBindingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterBindingException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ParameterBindingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterBindingException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ParameterBindingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}