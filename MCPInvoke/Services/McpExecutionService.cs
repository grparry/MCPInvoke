using MCPInvoke.Models; 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
    /// <param name="toolDefinitionProvider">The provider used to get tool definitions for registration.</param>
    /// <param name="logger">The logger for logging messages.</param>
    public McpExecutionService(
        IServiceScopeFactory serviceScopeFactory, 
        IMcpToolDefinitionProvider toolDefinitionProvider,
        ILogger<McpExecutionService> logger) 
    {
        _serviceScopeFactory = serviceScopeFactory;
        _toolDefinitionProvider = toolDefinitionProvider; 
        _logger = logger; 

        PopulateToolRegistryFromProvider(toolDefinitionProvider);

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false 
        };
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

            // Handle MCP-style requests (method = "tools/call")
            if (request.Method == "tools/call")
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
            object?[] callParameters = new object[methodParameters.Length];

            _logger.LogInformation("  Attempting to bind parameters for {MethodName}. C# method expects {ParamCount} parameters.", 
                                 toolName, methodParameters.Length);
            _logger.LogInformation("  Registered schema keys for {MethodName} at call time: {Keys}", 
                                 toolName, string.Join(", ", registeredTool.InputParameters.Keys));

            for (int i = 0; i < methodParameters.Length; i++)
            {
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
                            callParameters[i] = paramInfo.DefaultValue;
                            _logger.LogInformation("      Using C# default value for optional param '{ParamName}' as it's not in schema or RPC params.", paramNameFromReflection);
                            continue;
                        }
                        callParameters[i] = paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType) : null;
                        _logger.LogInformation("      Using CLR default for optional param '{ParamName}' as it's not in schema or RPC params and has no C# default.", paramNameFromReflection);
                        continue;
                    }
                    _logger.LogWarning("Parameter '{ParamName}' is present in RPC call but not defined in tool schema. Attempting to bind anyway as it's C# optional.", paramNameFromReflection);
                }

                JsonElement paramValueJson = default;
                bool hasParamValue = hasParamsElement && paramsElement.TryGetProperty(paramNameFromReflection, out paramValueJson);
                
                if (!hasParamValue)
                {
                    if (schemaParam != null && schemaParam.IsRequired) 
                    {
                        var error = new JsonRpcError { Code = -32602, Message = $"Invalid params: Missing required parameter '{paramNameFromReflection}' for method '{toolName}'." };
                        return JsonSerializer.Serialize(new JsonRpcResponse(responseId, error: error), _jsonSerializerOptions);
                    }
                    else if (paramInfo.HasDefaultValue) 
                    {
                        callParameters[i] = paramInfo.DefaultValue;
                        _logger.LogInformation("      Using C# default value for '{ParamName}'.", paramNameFromReflection);
                    }
                    else 
                    {
                        callParameters[i] = paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType) : null;
                        _logger.LogInformation("      Using CLR default for '{ParamName}'.", paramNameFromReflection);
                    }
                }
                else
                {
                    try
                    {
                        Type targetType = schemaParam?.ClrType ?? paramInfo.ParameterType;
                        callParameters[i] = JsonSerializer.Deserialize(paramValueJson.GetRawText(), targetType, _jsonSerializerOptions);
                        _logger.LogInformation("      Successfully bound '{ParamName}' from RPC params.", paramNameFromReflection);
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
                        result = taskResult.GetType().GetProperty("Result")?.GetValue(taskResult);
                    }
                    else 
                    {
                        result = null; 
                    }
                }
                _logger.LogInformation("Method {MethodName} executed successfully.", toolName);
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
}
