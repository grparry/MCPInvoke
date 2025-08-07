using MCPInvoke.Models; 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MCPInvoke.Services;

/// <summary>
/// Service responsible for handling the execution of MCP tool requests.
/// </summary>
public class McpExecutionService
{
    private readonly IServiceScopeFactory _serviceScopeFactory; 
    private readonly IMcpToolDefinitionProvider _toolDefinitionProvider; 
    private readonly ConcurrentDictionary<string, RegisteredTool> _toolRegistry = new();
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger<McpExecutionService> _logger; 

    /// <summary>
    /// Initializes a new instance of the <see cref="McpExecutionService"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The factory used to create scopes for resolving services.</param>
    /// <param name="toolDefinitionProvider">The provider that supplies tool definitions to be registered.</param>
    /// <param name="logger">The logger for logging messages.</param>
    public McpExecutionService(
        IServiceScopeFactory serviceScopeFactory,
        IMcpToolDefinitionProvider toolDefinitionProvider,
        ILogger<McpExecutionService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _toolDefinitionProvider = toolDefinitionProvider ?? throw new ArgumentNullException(nameof(toolDefinitionProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure JSON serialization options to properly handle string enums
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            // Add JsonStringEnumConverter to handle string enum serialization
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        
        _logger.LogInformation("McpExecutionService initialized with JsonStringEnumConverter");
        
        // Populate tool registry from provider
        PopulateToolRegistryFromProvider(toolDefinitionProvider);
    }

    /// <summary>
    /// Registers a tool, making it available for execution.
    /// If a tool with the same name already exists, it will be overwritten.
    /// </summary>
    /// <param name="tool">The <see cref="RegisteredTool"/> to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tool"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="tool.ToolName"/> is null or whitespace.</exception>
    public void RegisterTool(RegisteredTool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        if (string.IsNullOrWhiteSpace(tool.ToolName)) throw new ArgumentException("Tool name cannot be empty.", nameof(tool.ToolName));

        _toolRegistry[tool.ToolName] = tool;
        _logger.LogInformation("Successfully registered tool: {ToolName}", tool.ToolName); 
    }

    private void PopulateToolRegistryFromProvider(IMcpToolDefinitionProvider toolDefinitionProvider)
    {
        _logger.LogInformation("Populating tool registry from provider...");
        if (toolDefinitionProvider == null)
        {
            _logger.LogWarning("IMcpToolDefinitionProvider is null. No tools will be populated from provider.");
            return;
        }

        foreach (var mcpToolDef in toolDefinitionProvider.GetToolDefinitions())
        {
            _logger.LogInformation("Processing tool definition: {ToolName}", mcpToolDef.Name);
            try
            {
                // If GetType with throwOnError:true doesn't throw, handlerType is non-null.
                Type handlerType = Type.GetType(mcpToolDef.HandlerTypeAssemblyQualifiedName, throwOnError: true)!; 
                // Removed: if (handlerType == null) check, as GetType would throw.

                MethodInfo? methodInfo = handlerType.GetMethod(mcpToolDef.MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (methodInfo == null)
                {
                    _logger.LogError("Error: Could not find method '{MethodName}' on type '{HandlerFullName}' for tool '{ToolName}'. Skipping.", 
                                     mcpToolDef.MethodName, handlerType.FullName, mcpToolDef.Name);
                    continue;
                }

                var inputParams = new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>();
                _logger.LogInformation("  Input schema for {ToolName}:", mcpToolDef.Name);
                foreach (var paramDef in mcpToolDef.InputSchema)
                {
                    _logger.LogInformation("    Schema param: Name='{ParamName}', Type='{ParamType}', IsRequired={IsRequired}, Length={ParamNameLength}", 
                                         paramDef.Name, paramDef.Type, paramDef.IsRequired, paramDef.Name?.Length ?? -1);
                    
                    if (string.IsNullOrEmpty(paramDef.Name))
                    {
                        _logger.LogError("Error: Parameter definition for tool '{ToolName}' has a null or empty name. Skipping this parameter.", mcpToolDef.Name);
                        continue;
                    }

                    Type clrType = MapJsonTypeToClrType(paramDef.Type);
                    inputParams[paramDef.Name] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(clrType, paramDef.IsRequired, null);
                }
                _logger.LogInformation("  Registered schema keys for {ToolName}: {Keys}", 
                                     mcpToolDef.Name, string.Join(", ", inputParams.Keys));

                bool isStatic = methodInfo.IsStatic;
                // For static methods, methodInfo.DeclaringType is non-null and represents the type declaring the method.
                // For instance methods, handlerType is the resolved instance type.
                Type effectiveDeclaringType = isStatic ? methodInfo.DeclaringType! : handlerType;

                var registeredTool = new RegisteredTool(mcpToolDef.Name, methodInfo, effectiveDeclaringType, inputParams);
                _toolRegistry[registeredTool.ToolName] = registeredTool;
                _logger.LogInformation("Successfully registered tool from provider: {ToolName}", registeredTool.ToolName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register tool '{ToolName}' from provider.", mcpToolDef.Name);
            }
        }
    }

    private Type MapJsonTypeToClrType(string jsonType)
    {
        return jsonType.ToLowerInvariant() switch
        {
            "string" => typeof(string),
            "integer" => typeof(int), 
            "number" => typeof(double), 
            "boolean" => typeof(bool),
            "array" => typeof(object[]), 
            "object" => typeof(object),  
            _ => typeof(object) 
        };
    }

    /// <summary>
    /// Processes an incoming JSON-RPC request string, executes the corresponding tool method, and returns a JSON-RPC response string.
    /// </summary>
    /// <param name="jsonRpcRequestString">The raw JSON-RPC request string.</param>
    /// <returns>A task representing the asynchronous operation, with a result of the JSON-RPC response string.</returns>
    public async Task<string> ProcessRequestAsync(string jsonRpcRequestString)
    {
        JsonRpcRequest? request = null;
        JsonElement? responseId = null; // Use this for constructing JsonRpcResponse
        JsonElement paramsElement = default; // To handle both standard and MCP formats
        bool hasParamsElement = false; // Track if we have valid parameters
        string toolName = string.Empty; // The actual tool name to execute

        try
        {
            // Try to parse out the ID first for error reporting, even if full request is malformed.
            using (var doc = JsonDocument.Parse(jsonRpcRequestString))
            {
                if (doc.RootElement.TryGetProperty("id", out var idElement))
                {
                    responseId = idElement.Clone(); 
                }
            }
        }
        catch (JsonException) { /* responseId remains null if 'id' parsing fails */ }

        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(jsonRpcRequestString, _jsonSerializerOptions);
            if (request == null) throw new JsonException("Failed to deserialize JSON-RPC request or request is null.");
            
            // For logging, convert the JsonElement ID to a more readable form if possible
            object? loggingRequestId = request.Id switch
            {
                JsonElement element when element.ValueKind == JsonValueKind.Number => element.TryGetInt64(out long l) ? l : (object?)element.ToString(),
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                _ => null 
            };

            // Handle MCP-style requests
            if (request.Method == "tools/list")
            {
                _logger.LogInformation("Processing tools/list request");
                
                // Return the list of available tools
                var tools = _toolDefinitionProvider.GetToolDefinitions().Select(tool => new
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = new
                    {
                        type = "object",
                        properties = tool.InputSchema.ToDictionary(
                            param => param.Name,
                            param =>
                            {
                                var props = new Dictionary<string, object>
                                {
                                    ["type"] = param.Type.ToLowerInvariant()
                                };
                                
                                // Only include description if it's not null
                                if (!string.IsNullOrEmpty(param.Description))
                                {
                                    props["description"] = param.Description;
                                }
                                
                                return props;
                            }
                        ),
                        required = tool.InputSchema.Where(p => p.IsRequired).Select(p => p.Name).ToArray()
                    }
                }).ToArray();
                
                var result = new { tools };
                return JsonSerializer.Serialize(new JsonRpcResponse(responseId, result: result), _jsonSerializerOptions);
            }
            else if (request.Method == "tools/call")
            {
                _logger.LogInformation("Processing MCP-compliant request with method: tools/call, ID: {RequestId}", loggingRequestId);
                
                // Extract the tool name from params.name
                if (request.Params == null)
                {
                    var error = new JsonRpcError { Code = -32602, Message = "Invalid params: Missing params in MCP request" };
                    return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                }
                
                if (!request.Params.Value.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String)
                {
                    var error = new JsonRpcError { Code = -32602, Message = "Invalid params: Missing or invalid 'name' property in MCP request" };
                    return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                }
                
                toolName = nameElement.GetString() ?? string.Empty;
                
                // Extract the arguments from params.arguments
                if (request.Params.Value.TryGetProperty("arguments", out var argumentsElement) &&
                    argumentsElement.ValueKind == JsonValueKind.Object)
                {
                    paramsElement = argumentsElement.Clone();
                    hasParamsElement = true;
                }
                else
                {
                    // If no arguments property or not an object, use an empty object
                    paramsElement = JsonDocument.Parse("{}").RootElement.Clone();
                    hasParamsElement = true;
                }
            }
            else
            {
                // Legacy format - method is the tool name, params are direct
                _logger.LogInformation("Processing legacy JSON-RPC request for method: {MethodName}, ID: {RequestId}", request.Method, loggingRequestId);
                toolName = request.Method;
                
                if (request.Params != null)
                {
                    paramsElement = request.Params.Value.Clone();
                    hasParamsElement = true;
                }
                else
                {
                    hasParamsElement = false;
                }
            }

            _logger.LogInformation("Tool to execute: {ToolName}", toolName);

            if (!_toolRegistry.TryGetValue(toolName, out RegisteredTool? registeredTool) || registeredTool == null)
            {
                var error = new JsonRpcError { Code = -32601, Message = $"Method '{toolName}' not found" };
                return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
            }

            ParameterInfo[] methodParameters = registeredTool.Method.GetParameters();
            _logger.LogInformation("  Attempting to bind parameters for {MethodName}. C# method expects {ParamCount} parameters.", 
                                 toolName, methodParameters.Length);
            _logger.LogInformation("  Registered schema keys for {MethodName} at call time: {Keys}", 
                                 toolName, string.Join(", ", registeredTool.InputParameters.Keys));

            // Enhanced parameter binding with source detection support
            object?[]? callParameters = await BindParametersWithSourceDetection(methodParameters, registeredTool, paramsElement, hasParamsElement, toolName, responseId);
            
            if (callParameters == null)
            {
                // Check if this is a missing required parameter issue (should be -32602)
                // We need to determine if it's a missing required parameter vs other binding failure
                _logger.LogWarning("Parameter binding failed for method '{MethodName}' - checking for missing required parameters", toolName);
                
                // Re-check for missing required parameters to provide proper error code
                foreach (var paramInfo in methodParameters)
                {
                    string paramName = paramInfo.Name!;
                    if (registeredTool.InputParameters.TryGetValue(paramName, out var schemaParam))
                    {
                        // Check if this parameter is required but missing from request
                        if (schemaParam.IsRequired && (!hasParamsElement || !paramsElement.TryGetProperty(paramName, out _)))
                        {
                            var error = new JsonRpcError { Code = -32602, Message = $"Invalid params: Missing required parameter '{paramName}' for method '{toolName}'." };
                            return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                        }
                    }
                }
                
                // If not missing required parameter, then it's a generic binding failure
                return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: new JsonRpcError { Code = -32603, Message = "Parameter binding failed" }), _jsonSerializerOptions);
            }

            // Legacy parameter binding for backward compatibility
            for (int i = 0; i < methodParameters.Length; i++)
            {
                if (callParameters![i] != null) continue; // Skip parameters already bound by enhanced logic
                
                ParameterInfo paramInfo = methodParameters[i];
                string paramNameFromReflection = paramInfo.Name!;

                _logger.LogInformation("    C# param #{Index}: Name='{ParamName}', Type='{ParamType}', Optional={IsOptional}, Length={ParamNameLength}", 
                                     i, paramNameFromReflection, paramInfo.ParameterType.Name, paramInfo.IsOptional, paramNameFromReflection.Length);

                if (!registeredTool.InputParameters.TryGetValue(paramNameFromReflection, out var schemaParam))
                {
                    if (!paramInfo.IsOptional && !paramInfo.HasDefaultValue) 
                    {
                        _logger.LogError("Parameter '{ParamName}' for method '{MethodName}' is required by C# signature but no schema was provided for it.", 
                                         paramNameFromReflection, toolName);
                        var error = new JsonRpcError { Code = -32603, Message = $"Internal error: Parameter '{paramNameFromReflection}' schema not found for method '{toolName}'." };
                        return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                    }
                    if (!hasParamsElement || !paramsElement.TryGetProperty(paramNameFromReflection, out _))
                    {
                        if (paramInfo.HasDefaultValue)
                        {
                            callParameters![i] = paramInfo.DefaultValue;
                            _logger.LogInformation("      Using C# default value for optional param '{ParamName}' as it's not in schema or RPC params.", paramNameFromReflection);
                            continue;
                        }
                        callParameters![i] = paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType)! : null;
                        _logger.LogInformation("      Using CLR default for optional param '{ParamName}' as it's not in schema or RPC params and has no C# default.", paramNameFromReflection);
                        continue;
                    }
                    _logger.LogWarning("Parameter '{ParamName}' is present in RPC call but not defined in tool schema. Attempting to bind anyway as it's C# optional.", paramNameFromReflection);
                }

                JsonElement paramValueJson = default;
                bool hasParamValue = hasParamsElement && paramsElement.TryGetProperty(paramNameFromReflection, out paramValueJson);
                
                if (!hasParamValue)
                {
                    // First check if the C# parameter has a default value, which means it's optional
                    // This takes precedence over the schema definition
                    if (paramInfo.HasDefaultValue) 
                    {
                        callParameters![i] = paramInfo.DefaultValue;
                        _logger.LogInformation("      Using default value for optional param '{ParamName}' which was not provided in request.", paramNameFromReflection);
                    }
                    // Only treat as required if it doesn't have a default value AND the schema marks it as required
                    else if (schemaParam != null && schemaParam.IsRequired) 
                    {
                        var error = new JsonRpcError { Code = -32602, Message = $"Invalid params: Missing required parameter '{paramNameFromReflection}' for method '{toolName}'." };
                        return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                    }
                    else 
                    {
                        callParameters![i] = paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType)! : null;
                        _logger.LogInformation("      Using CLR default for '{ParamName}'.", paramNameFromReflection);
                    }
                }
                else
                {
                    try
                    {
                        Type targetType = schemaParam?.ClrType ?? paramInfo.ParameterType;
                        
                        // CRITICAL FIX: Check if the parameter is an enum but targetType isn't recognizing it
                        // This specifically addresses the issue where an enum is being treated as Int32
                        // IMPORTANT: We only want to apply this fix to actual enum types, not to complex objects
                        // that might contain enum properties (like the LLM request object)
                        if (paramInfo.ParameterType.IsEnum && targetType == typeof(int) 
                            && !paramInfo.ParameterType.IsClass && !paramInfo.ParameterType.IsInterface)
                        {
                            _logger.LogWarning("      CORRECTING TYPE MISMATCH: Parameter '{ParamName}' is enum type '{EnumType}' but was incorrectly mapped to Int32",
                                              paramNameFromReflection, paramInfo.ParameterType.Name);
                                              
                            // Override targetType to be the actual enum type
                            targetType = paramInfo.ParameterType;
                            _logger.LogInformation("      Corrected target type to enum: {EnumType}", targetType.Name);
                        }
                        
                        // Special handling for enum types
                        if (targetType.IsEnum)
                        {
                            _logger.LogInformation("      Special handling for ENUM parameter '{ParamName}' with type '{EnumType}'", 
                                                  paramNameFromReflection, targetType.Name);
                                                  
                            // Check if the enum type is decorated with JsonStringEnumConverter
                            bool usesStringEnumConverter = targetType.GetCustomAttributes(true)
                                .Any(attr => attr.GetType().Name.Contains("JsonStringEnumConverter"));
                            
                            // Also check if the parameter is decorated with JsonStringEnumConverter
                            var parameterAttributes = paramInfo.GetCustomAttributes(true);
                            bool paramUsesStringEnum = parameterAttributes.Any(attr => attr.GetType().Name.Contains("JsonStringEnumConverter"));
                            
                            // Log detailed debugging info
                            _logger.LogInformation("      Enum '{EnumType}' type-level JsonStringEnumConverter: {TypeConverter}, parameter-level: {ParamConverter}", 
                                                 targetType.Name, usesStringEnumConverter, paramUsesStringEnum);
                                                 
                            // Combine attributes - if either the type or parameter has the converter, treat as string enum
                            usesStringEnumConverter = usesStringEnumConverter || paramUsesStringEnum;
                            
                            // Detailed logging of enum properties and attributes
                            _logger.LogInformation("      DETAILED ENUM DIAGNOSTICS for '{ParamName}' of type '{EnumType}'", 
                                               paramNameFromReflection, targetType.Name);
                            _logger.LogInformation("      - JsonSerializerOptions Contains JsonStringEnumConverter: {HasConverter}", 
                                               _jsonSerializerOptions.Converters.Any(c => c.GetType().Name.Contains("JsonStringEnumConverter")));
                            
                            // Log all attributes on the parameter type
                            var typeAttrs = targetType.GetCustomAttributes(true);
                            foreach (var attr in typeAttrs)
                            {
                                _logger.LogInformation("      - Type Attribute: {AttributeType}", attr.GetType().FullName);
                            }
                            
                            // Log all attributes on the parameter
                            var paramAttrs = paramInfo.GetCustomAttributes(true);
                            foreach (var attr in paramAttrs)
                            {
                                _logger.LogInformation("      - Parameter Attribute: {AttributeType}", attr.GetType().FullName);
                            }
                            
                            // Log the actual JsonElement for the parameter
                            _logger.LogInformation("      - Raw JSON value: {RawJson}, Type: {ValueKind}", 
                                               paramValueJson.GetRawText(), paramValueJson.ValueKind);
                            
                            // CRITICAL FIX: Create a custom JsonSerializerOptions specifically for enum parameters
                            // with JsonStringEnumConverter that ensures enum values can be properly deserialized
                            var enumSerializerOptions = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
                            };
                            _logger.LogInformation("      Created custom JsonSerializerOptions with JsonStringEnumConverter specifically for enum parameters");
                            
                            // Multi-strategy approach to handle enum parameters properly
                            _logger.LogInformation("      Using multi-strategy approach for enum parameter binding");
                            
                            // IMPROVED STRATEGY 1: Try direct deserialization with custom enum serializer options
                            // The key difference is we'll wrap the string value in quotes if it's a string-based enum
                            if (paramValueJson.ValueKind == JsonValueKind.String && usesStringEnumConverter)
                            {
                                try
                                {
                                    string enumStringValue = paramValueJson.GetString() ?? string.Empty;
                                    _logger.LogInformation("      ENHANCED STRATEGY 1: Direct string deserialization with JsonStringEnumConverter for string '{EnumValue}'", enumStringValue);
                                    
                                    // Manually format JSON that JsonStringEnumConverter can understand - this is the crucial fix
                                    string jsonString = $"\"{enumStringValue}\"";
                                    _logger.LogInformation("      Using specifically formatted JSON string: {JsonString}", jsonString);
                                    
                                    callParameters![i] = JsonSerializer.Deserialize(jsonString, targetType, enumSerializerOptions) ?? GetDefaultValueForType(targetType);
                                    _logger.LogInformation("      SUCCESS: Bound enum parameter '{ParamName}' using enhanced string deserialization with JsonStringEnumConverter", 
                                                          paramNameFromReflection);
                                    continue; // Continue to next parameter if success
                                }
                                catch (Exception directEx)
                                {
                                    _logger.LogWarning("      ENHANCED STRATEGY 1 FAILED: {Error}", directEx.Message);
                                }
                            }
                            else
                            {
                                // Standard attempt for non-string or non-JsonStringEnumConverter enums
                                try
                                {
                                    _logger.LogInformation("      STRATEGY 1: Direct deserialization with JsonStringEnumConverter");
                                    callParameters![i] = JsonSerializer.Deserialize(paramValueJson.GetRawText(), targetType, enumSerializerOptions) ?? GetDefaultValueForType(targetType);
                                    _logger.LogInformation("      SUCCESS: Bound enum parameter '{ParamName}' using direct deserialization with JsonStringEnumConverter", 
                                                          paramNameFromReflection);
                                    continue; // Continue to next parameter if success
                                }
                                catch (Exception directEx)
                                {
                                    _logger.LogWarning("      STRATEGY 1 FAILED: {Error}", directEx.Message);
                                }
                            }

                            // Handle different JSON value kinds for enums - multiple strategies
                            if (paramValueJson.ValueKind == JsonValueKind.String)
                            {
                                string enumStringValue = paramValueJson.GetString() ?? string.Empty;
                                _logger.LogInformation("      STRATEGY 2: Processing enum parameter with string value: '{EnumValue}'", enumStringValue);
                                
                                // Strategy 2: Try case-insensitive enum parsing
                                try
                                {
                                    _logger.LogInformation("      STRATEGY 2A: Direct Enum.Parse with case-insensitive matching");
                                    callParameters![i] = Enum.Parse(targetType, enumStringValue, ignoreCase: true);
                                    _logger.LogInformation("      SUCCESS: Bound enum '{ParamName}' with case-insensitive match", paramNameFromReflection);
                                    continue; // Continue to next parameter if success
                                }
                                catch (Exception enumEx)
                                {
                                    _logger.LogWarning("      STRATEGY 2A FAILED: {Error}", enumEx.Message);
                                }
                                
                                // Strategy 3: Try manual string matching against enum names
                                try
                                {
                                    _logger.LogInformation("      STRATEGY 3: Manual string matching against enum names");
                                    var enumValues = Enum.GetNames(targetType);
                                    var caseInsensitiveMatch = enumValues.FirstOrDefault(name => 
                                        string.Equals(name, enumStringValue, StringComparison.OrdinalIgnoreCase));
                                        
                                    if (caseInsensitiveMatch != null)
                                    {
                                        _logger.LogInformation("      SUCCESS: Found case-insensitive enum name match: '{Original}' → '{Match}'", 
                                                             enumStringValue, caseInsensitiveMatch);
                                        callParameters![i] = Enum.Parse(targetType, caseInsensitiveMatch);
                                        continue; // Continue to next parameter if success
                                    }
                                    _logger.LogWarning("      STRATEGY 3 FAILED: No matching enum name found");
                                }
                                catch (Exception manualEx)
                                {
                                    _logger.LogWarning("      STRATEGY 3 FAILED with exception: {Error}", manualEx.Message);
                                }
                                
                                // Strategy 4: Try numeric parsing if the string contains a number
                                try
                                {
                                    _logger.LogInformation("      STRATEGY 4: Try parsing string as numeric enum value");
                                    if (int.TryParse(enumStringValue, out var enumIntValue))
                                    {
                                        _logger.LogInformation("      Converting string '{Value}' to numeric enum value {IntValue}", 
                                                           enumStringValue, enumIntValue);
                                        callParameters![i] = Enum.ToObject(targetType, enumIntValue);
                                        continue; // Continue to next parameter if success
                                    }
                                    _logger.LogWarning("      STRATEGY 4 FAILED: String is not a valid numeric value");
                                }
                                catch (Exception numericEx)
                                {
                                    _logger.LogWarning("      STRATEGY 4 FAILED with exception: {Error}", numericEx.Message);
                                }
                            }
                            else if (paramValueJson.ValueKind == JsonValueKind.Number)
                            {
                                // Strategy 5: Handle numeric enum values
                                try
                                {
                                    _logger.LogInformation("      STRATEGY 5: Processing numeric enum value");
                                    int enumIntValue = paramValueJson.GetInt32();
                                    
                                    // For all enums, just convert the number to the enum regardless of JsonStringEnumConverter
                                    // This simplifies the logic and is more reliable
                                    callParameters![i] = Enum.ToObject(targetType, enumIntValue);
                                    _logger.LogInformation("      SUCCESS: Bound enum '{ParamName}' with numeric value {Value}", 
                                                         paramNameFromReflection, enumIntValue);
                                    continue; // Continue to next parameter if success
                                }
                                catch (Exception enumEx)
                                {
                                    _logger.LogError("      STRATEGY 5 FAILED with error: {Error}", enumEx.Message);
                                }
                            }
                            
                            // Final fallback: Use standard deserialization
                            _logger.LogWarning("      ALL STRATEGIES FAILED! Fallback to default deserializer for parameter '{ParamName}'", 
                                              paramNameFromReflection);
                            callParameters![i] = JsonSerializer.Deserialize(paramValueJson.GetRawText(), targetType, _jsonSerializerOptions) ?? GetDefaultValueForType(targetType);
                        }
                        else
                        {
                            // For non-enum types, check if we need to handle a complex object with enums
                            if (targetType.IsClass || targetType.IsInterface || targetType.IsValueType)
                            {
                                try
                                {
                                    _logger.LogInformation("      Binding complex parameter '{ParamName}' of type '{Type}'", 
                                                       paramNameFromReflection, targetType.Name);
                                    
                                    // CRITICAL: Check if the parameter type is an enum or contains enum properties
                                    // This handles cases where the proxy/controller is expecting an enum but we're getting a raw value
                                    if (targetType.IsEnum || targetType.GetProperties().Any(p => p.PropertyType.IsEnum))
                                    {
                                        _logger.LogInformation("      Parameter '{ParamName}' is of enum type or contains enum properties. Using specialized handling.",
                                             paramNameFromReflection);
                                             
                                        // Check if the enum type is decorated with JsonStringEnumConverter
                                        bool usesStringEnumConverter = targetType.GetCustomAttributes(true)
                                            .Any(attr => attr.GetType().Name.Contains("JsonStringEnumConverter"));
                                            
                                        if (usesStringEnumConverter && paramValueJson.ValueKind == JsonValueKind.String)
                                        {
                                            string enumStringValue = paramValueJson.GetString() ?? string.Empty;
                                            _logger.LogInformation("      Using specialized enum handling for string value '{EnumValue}'", enumStringValue);
                                            
                                            // Try Enum.Parse with the string value
                                            try 
                                            {
                                                callParameters![i] = Enum.Parse(targetType, enumStringValue, ignoreCase: true);
                                                _logger.LogInformation("      Successfully parsed enum string value '{EnumValue}' to {EnumType}", 
                                                                     enumStringValue, targetType.Name);
                                                continue;
                                            }
                                            catch (Exception enumEx)
                                            {
                                                _logger.LogWarning("      Failed to directly parse string to enum: {Error}", enumEx.Message);
                                            }
                                        }
                                    }
                                    
                                    // CRITICAL FIX: Use the actual parameter type from the method signature instead of the mapped type
                                    // This ensures we deserialize to the correct type expected by the controller method
                                    Type actualTargetType = paramInfo.ParameterType;
                                    
                                    _logger.LogInformation("      Using actual parameter type '{ActualType}' instead of mapped type '{MappedType}' for complex object deserialization", 
                                                         actualTargetType.Name, targetType.Name);
                                    
                                    // Attempt standard deserialization with our JsonSerializerOptions
                                    // that has JsonStringEnumConverter registered, using the actual parameter type
                                    callParameters![i] = JsonSerializer.Deserialize(paramValueJson.GetRawText(), actualTargetType, _jsonSerializerOptions) ?? GetDefaultValueForType(actualTargetType);
                                    _logger.LogInformation("      Successfully bound complex parameter '{ParamName}' to actual type '{ActualType}'", 
                                                         paramNameFromReflection, actualTargetType.Name);
                                }
                                catch (Exception complexEx)
                                {
                                    _logger.LogError(complexEx, "Failed to deserialize complex parameter '{ParamName}' to type '{TargetType}'. Raw JSON: {RawJson}", 
                                                   paramNameFromReflection, paramInfo.ParameterType.Name, paramValueJson.GetRawText());
                                    
                                    // Enhanced error message for complex object deserialization failures
                                    var error = new JsonRpcError 
                                    { 
                                        Code = -32000, 
                                        Message = $"Server error: Object of type 'System.Text.Json.JsonElement' cannot be converted to type '{paramInfo.ParameterType.FullName}'. " +
                                                $"Parameter: '{paramNameFromReflection}'. Error: {complexEx.Message}" 
                                    };
                                    return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                                }
                            }
                            else
                            {
                                // Standard deserialization for simple non-enum types
                                callParameters![i] = JsonSerializer.Deserialize(paramValueJson.GetRawText(), targetType, _jsonSerializerOptions) ?? GetDefaultValueForType(targetType);
                                _logger.LogInformation("      Successfully bound '{ParamName}' from RPC params.", paramNameFromReflection);
                            }
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization error for parameter '{ParamName}'.", paramNameFromReflection);
                        var error = new JsonRpcError { Code = -32602, Message = $"Invalid params: Type mismatch or invalid format for parameter '{paramNameFromReflection}' for method '{toolName}'. Expected type compatible with '{paramInfo.ParameterType.Name}'. Error: {jsonEx.Message}" };
                        return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                    }
                }
            }

            object? serviceInstance = null;
            IServiceScope? scope = null;
            if (!registeredTool.Method.IsStatic)
            {
                scope = _serviceScopeFactory.CreateScope();
                try
                {
                    // First attempt: try to get the service directly (works for singleton/scoped/transient services)
                    serviceInstance = scope.ServiceProvider.GetRequiredService(registeredTool.DeclaringType!);
                    _logger.LogInformation("Successfully resolved service: {ServiceType}", registeredTool.DeclaringType!.FullName);
                }
                catch (InvalidOperationException ex)
                {
                    // Log the exception for debugging purposes
                    _logger.LogDebug(ex, "Could not resolve service of type {ServiceType} directly from DI container.", registeredTool.DeclaringType!.FullName);
                    
                    // Second attempt: For ASP.NET Core controllers that aren't registered directly
                    if (typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(registeredTool.DeclaringType))
                    {
                        _logger.LogInformation("Attempting to create controller instance directly: {ControllerType}", registeredTool.DeclaringType!.FullName);
                        try
                        {
                            // Create controller instance with constructor injection, manually resolving dependencies
                            var constructor = registeredTool.DeclaringType.GetConstructors()
                                .OrderByDescending(c => c.GetParameters().Length)
                                .FirstOrDefault();

                            if (constructor != null)
                            {
                                var parameters = constructor.GetParameters();
                                var parameterInstances = new object[parameters.Length];

                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    parameterInstances[i] = scope.ServiceProvider.GetRequiredService(parameters[i].ParameterType);
                                }

                                serviceInstance = constructor.Invoke(parameterInstances);
                                _logger.LogInformation("Successfully created controller instance: {ControllerType}", registeredTool.DeclaringType!.FullName);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Could not find suitable constructor for {registeredTool.DeclaringType!.FullName}");
                            }
                        }
                        catch (Exception ctorEx)
                        {
                            throw new InvalidOperationException($"Failed to instantiate controller {registeredTool.DeclaringType!.FullName}", ctorEx);
                        }
                    }
                    else
                    {
                        // Re-throw the original exception for non-controller services
                        throw;
                    }
                }
            }

            try
            {
                object? result = registeredTool.Method.Invoke(serviceInstance, callParameters);

                if (result is Task taskResult)
                {
                    await taskResult.ConfigureAwait(false);
                    if (taskResult.GetType().IsGenericType) 
                    {
                        // Get the raw Task<T>.Result value
                        var taskResultValue = taskResult.GetType().GetProperty("Result")?.GetValue(taskResult);
                        
                        // Check if the result is from ASP.NET Core (ActionResult<T>, ObjectResult, etc.)
                        var resultType = taskResultValue?.GetType();
                        if (resultType != null)
                        {
                            _logger.LogInformation("Processing return type: {ResultType}", resultType.FullName);
                            
                            // Handle ActionResult<T> wrapper
                            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition().Name.StartsWith("ActionResult"))
                            {
                                _logger.LogInformation("Detected ActionResult<T> return type...");
                                // Get the Result property if it exists (this would be an IActionResult)
                                var resultProperty = resultType.GetProperty("Result");
                                if (resultProperty != null)
                                {
                                    taskResultValue = resultProperty.GetValue(taskResultValue);
                                    _logger.LogInformation("Found Result property, new type: {ResultType}", 
                                        taskResultValue?.GetType()?.FullName ?? "null");
                                    resultType = taskResultValue?.GetType();
                                }
                            }
                            
                            // Now handle IActionResult implementations (OkObjectResult, NotFoundResult, etc.)
                            if (resultType != null && typeof(Microsoft.AspNetCore.Mvc.IActionResult).IsAssignableFrom(resultType))
                            {
                                _logger.LogInformation("Processing IActionResult type: {ResultType}", resultType.FullName);
                                
                                // Check for ObjectResult and its derivatives (OkObjectResult, BadRequestObjectResult, etc.)
                                if (typeof(Microsoft.AspNetCore.Mvc.ObjectResult).IsAssignableFrom(resultType))
                                {
                                    _logger.LogInformation("Found ObjectResult type, extracting Value property");
                                    var valueProperty = resultType.GetProperty("Value");
                                    if (valueProperty != null)
                                    {
                                        result = valueProperty.GetValue(taskResultValue);
                                        _logger.LogInformation("Extracted Value from ObjectResult: {ValueType}", 
                                            result?.GetType()?.FullName ?? "null");
                                    }
                                    else
                                    {
                                        result = taskResultValue;
                                    }
                                }
                                else
                                {
                                    // For other action results (like StatusCodeResult), just use as-is
                                    result = taskResultValue;
                                }
                            }
                            else
                            {
                                // Regular object value, not an ActionResult
                                result = taskResultValue;
                            }
                        }
                        else
                        {
                            // If resultType is null, use the original taskResultValue
                            result = taskResultValue;
                        }
                    }
                    else 
                    {
                        result = null; 
                    }
                }
                _logger.LogInformation("Method {MethodName} executed successfully.", toolName);
                
                // Claude Code CLI expects MCP responses to follow MCP content schema
                // Format result as MCP content with type:"text" and stringified JSON data
                if (result != null)
                {
                    _logger.LogInformation("Formatting result for Claude Code CLI MCP content schema compliance.");
                    var jsonResult = JsonSerializer.Serialize(result, _jsonSerializerOptions);
                    result = new { 
                        content = new[] { 
                            new { 
                                type = "text", 
                                text = jsonResult 
                            } 
                        } 
                    };
                    _logger.LogInformation("Wrapped result in MCP content schema format with type='text'.");
                }
                
                return JsonSerializer.Serialize(new JsonRpcResponse(responseId, result: result), _jsonSerializerOptions);
            }
            catch (TargetInvocationException tie)
            {
                _logger.LogError(tie.InnerException ?? tie, "Error executing method '{MethodName}'.", toolName);
                var error = new JsonRpcError { Code = -32000, Message = $"Server error: {tie.InnerException?.Message ?? tie.Message}" }; 
                return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Generic error executing method '{MethodName}'.", toolName);
                var error = new JsonRpcError { Code = -32000, Message = $"Server error: {ex.Message}" };
                return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
            }
            finally
            {
                scope?.Dispose();
            }
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "JSON processing error.");
            var error = new JsonRpcError { Code = -32700, Message = $"Parse error: {jsonEx.Message}" };
            return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Unexpected error processing request.");
            var error = new JsonRpcError { Code = -32603, Message = $"Internal error: {ex.Message}" };
            return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
        }
    }

    /// <summary>
    /// Enhanced parameter binding with source detection support.
    /// Handles route parameters, complex objects, and proper type coercion.
    /// </summary>
    private async Task<object?[]?> BindParametersWithSourceDetection(
        ParameterInfo[] methodParameters, 
        RegisteredTool registeredTool, 
        JsonElement paramsElement, 
        bool hasParamsElement, 
        string toolName, 
        JsonElement? responseId)
    {
        object?[] callParameters = new object[methodParameters.Length];
        
        for (int i = 0; i < methodParameters.Length; i++)
        {
            ParameterInfo paramInfo = methodParameters[i];
            string paramNameFromReflection = paramInfo.Name!;
            
            _logger.LogInformation("Enhanced parameter binding for param #{Index}: Name='{ParamName}', Type='{ParamType}'", 
                                 i, paramNameFromReflection, paramInfo.ParameterType.Name);
            
            // Check if parameter is in registered schema
            if (!registeredTool.InputParameters.TryGetValue(paramNameFromReflection, out var schemaParam))
            {
                // Handle parameters not in schema (optional parameters, infrastructure types)
                if (IsAspNetCoreInfrastructureType(paramInfo.ParameterType))
                {
                    _logger.LogInformation("Skipping ASP.NET Core infrastructure parameter '{ParamName}'", paramNameFromReflection);
                    callParameters![i] = GetDefaultValueForType(paramInfo.ParameterType);
                    continue;
                }
                
                if (!paramInfo.IsOptional && !paramInfo.HasDefaultValue)
                {
                    _logger.LogError("Parameter '{ParamName}' for method '{MethodName}' is required but no schema was provided", 
                                   paramNameFromReflection, toolName);
                    return null; // Will trigger error in calling method
                }
                
                // Use default value for optional parameters not in schema
                callParameters![i] = paramInfo.HasDefaultValue ? paramInfo.DefaultValue : GetDefaultValueForType(paramInfo.ParameterType);
                continue;
            }
            
            // Check for parameter source information
            string? parameterSource = GetParameterSource(schemaParam);
            _logger.LogInformation("Parameter '{ParamName}' source: {Source}", paramNameFromReflection, parameterSource ?? "unknown");
            
            // Get parameter value from JSON
            JsonElement paramValueJson = default;
            bool hasParamValue = hasParamsElement && paramsElement.TryGetProperty(paramNameFromReflection, out paramValueJson);
            
            if (!hasParamValue)
            {
                // Handle missing parameters
                if (paramInfo.HasDefaultValue)
                {
                    callParameters![i] = paramInfo.DefaultValue;
                    _logger.LogInformation("Using default value for missing optional parameter '{ParamName}'", paramNameFromReflection);
                }
                else if (schemaParam.IsRequired)
                {
                    _logger.LogError("Missing required parameter '{ParamName}' for method '{MethodName}'", paramNameFromReflection, toolName);
                    return null; // Will trigger error in calling method
                }
                else
                {
                    callParameters![i] = GetDefaultValueForType(paramInfo.ParameterType);
                    _logger.LogInformation("Using CLR default for optional parameter '{ParamName}'", paramNameFromReflection);
                }
                continue;
            }
            
            // Bind parameter value with enhanced type handling
            try
            {
                callParameters![i] = await BindParameterValue(paramInfo, paramValueJson, schemaParam, parameterSource);
                _logger.LogInformation("Successfully bound parameter '{ParamName}' with enhanced binding", paramNameFromReflection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind parameter '{ParamName}' for method '{MethodName}'", paramNameFromReflection, toolName);
                return null; // Will trigger error in calling method
            }
        }
        
        return callParameters;
    }
    
    /// <summary>
    /// Gets parameter source from schema annotations.
    /// </summary>
    private string? GetParameterSource(RegisteredTool.McpToolSchemaPropertyPlaceholder schemaParam)
    {
        // This would be enhanced to read from schema annotations if they were available
        // For now, return null to indicate unknown source
        return null;
    }
    
    /// <summary>
    /// Checks if a type is an ASP.NET Core infrastructure type that should be skipped.
    /// </summary>
    private bool IsAspNetCoreInfrastructureType(Type type)
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
    /// Gets a default value for a given type.
    /// </summary>
    private object? GetDefaultValueForType(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }
    
    /// <summary>
    /// Binds a single parameter value with enhanced type handling.
    /// </summary>
    private Task<object?> BindParameterValue(
        ParameterInfo paramInfo, 
        JsonElement paramValueJson, 
        RegisteredTool.McpToolSchemaPropertyPlaceholder schemaParam, 
        string? parameterSource)
    {
        Type targetType = paramInfo.ParameterType;
        
        _logger.LogInformation("Binding parameter '{ParamName}' of type '{Type}' from source '{Source}'", 
                             paramInfo.Name, targetType.Name, parameterSource ?? "unknown");
        
        // Enhanced enum handling
        if (targetType.IsEnum)
        {
            return Task.FromResult(BindEnumParameter(targetType, paramValueJson, paramInfo.Name ?? "unknown"));
        }
        
        // Enhanced complex object handling
        if (IsComplexType(targetType))
        {
            return Task.FromResult(BindComplexObjectParameter(targetType, paramValueJson, paramInfo.Name ?? "unknown"));
        }
        
        // Standard primitive type binding
        return Task.FromResult(JsonSerializer.Deserialize(paramValueJson.GetRawText(), targetType, _jsonSerializerOptions));
    }
    
    /// <summary>
    /// Binds enum parameters with multiple fallback strategies.
    /// </summary>
    private object? BindEnumParameter(Type enumType, JsonElement paramValueJson, string paramName)
    {
        _logger.LogInformation("Binding enum parameter '{ParamName}' of type '{EnumType}'", paramName, enumType.Name);
        
        // Strategy 1: Direct string parsing
        if (paramValueJson.ValueKind == JsonValueKind.String)
        {
            string enumStringValue = paramValueJson.GetString() ?? string.Empty;
            
            // Try case-insensitive enum parsing
            if (Enum.TryParse(enumType, enumStringValue, true, out var enumResult))
            {
                _logger.LogInformation("Successfully parsed enum string '{Value}' to {EnumType}", enumStringValue, enumType.Name);
                return enumResult;
            }
        }
        
        // Strategy 2: Numeric value parsing
        if (paramValueJson.ValueKind == JsonValueKind.Number)
        {
            int enumIntValue = paramValueJson.GetInt32();
            var enumResult = Enum.ToObject(enumType, enumIntValue);
            _logger.LogInformation("Successfully converted numeric value {Value} to enum {EnumType}", enumIntValue, enumType.Name);
            return enumResult;
        }
        
        // Strategy 3: JsonSerializer with custom options
        var enumSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
        };
        
        return JsonSerializer.Deserialize(paramValueJson.GetRawText(), enumType, enumSerializerOptions);
    }
    
    /// <summary>
    /// Binds complex object parameters with proper deserialization.
    /// </summary>
    private object? BindComplexObjectParameter(Type objectType, JsonElement paramValueJson, string paramName)
    {
        _logger.LogInformation("Binding complex object parameter '{ParamName}' of type '{Type}'", paramName, objectType.Name);
        
        try
        {
            // Use the JsonSerializerOptions with JsonStringEnumConverter for complex objects
            return JsonSerializer.Deserialize(paramValueJson.GetRawText(), objectType, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize complex object '{ParamName}' of type '{Type}'", paramName, objectType.Name);
            throw new JsonException($"Failed to deserialize parameter '{paramName}' to type '{objectType.Name}': {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Determines if a type is a complex type requiring object deserialization.
    /// </summary>
    private bool IsComplexType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        // Primitive types and strings are not complex
        if (underlyingType.IsPrimitive || underlyingType == typeof(string) || 
            underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(TimeSpan) || underlyingType == typeof(Guid) ||
            underlyingType.IsEnum)
        {
            return false;
        }
        
        // Arrays and collections are handled separately
        if (type.IsArray || (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)))
        {
            return false;
        }
        
        // Everything else is considered complex
        return underlyingType.IsClass || underlyingType.IsInterface || underlyingType.IsValueType;
    }
}
