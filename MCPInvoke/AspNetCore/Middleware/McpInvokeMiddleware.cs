using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MCPInvoke.Models;
using MCPInvoke.Services;

namespace MCPInvoke.AspNetCore.Middleware
{
    /// <summary>
    /// Middleware to handle MCP tool invocation requests.
    /// </summary>
    public class McpInvokeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly McpExecutionService _executionService;
        private readonly ILogger<McpInvokeMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpInvokeMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="executionService">The MCP execution service used to process requests.</param>
        /// <param name="logger">The logger for logging information.</param>
        public McpInvokeMiddleware(
            RequestDelegate next,
            McpExecutionService executionService,
            ILogger<McpInvokeMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes an HTTP request to invoke an MCP tool.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Method == "POST" && context.Request.ContentType?.StartsWith("application/json") == true)
            {
                _logger.LogInformation("Processing MCP tool invocation request");
                
                // Read the request body
                string requestBody;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                // Log detailed request information
                _logger.LogInformation("Received JSON-RPC request: {Request}", requestBody);
                
                try
                {
                    // First check for special MCP protocol methods
                    string responseText = await HandleSpecialMcpMethodsAsync(requestBody);
                    
                    if (responseText == null)
                    {
                        // Not a special method, process normally using the execution service
                        responseText = await _executionService.ProcessRequestAsync(requestBody);
                    }
                    
                    // Log the response for debugging
                    _logger.LogInformation("JSON-RPC response: {Response}", responseText);
                    
                    // Write the response
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(responseText);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MCP tool invocation request");
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync("{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32603, \"message\": \"Internal error\"}, \"id\": null}");
                    return;
                }
            }
            
            // If not a POST request or not JSON content type, continue to the next middleware
            await _next(context);
        }

        /// <summary>
        /// Handles special MCP protocol methods like initialize, notifications/initialized, and tools/list.
        /// </summary>
        /// <param name="requestBody">The JSON-RPC request body.</param>
        /// <returns>The response JSON if handled, null otherwise.</returns>
        private async Task<string?> HandleSpecialMcpMethodsAsync(string requestBody)
        {
            try
            {
                using var requestJson = JsonDocument.Parse(requestBody);
                var root = requestJson.RootElement;

                // Check if it's a valid JSON-RPC request
                if (root.TryGetProperty("jsonrpc", out var jsonrpcVersion) &&
                    jsonrpcVersion.GetString() == "2.0" &&
                    root.TryGetProperty("method", out var methodElement))
                {
                    string method = methodElement.GetString() ?? string.Empty;
                    JsonElement? id = null;
                    if (root.TryGetProperty("id", out var idElement))
                    {
                        id = idElement.Clone();
                    }

                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

                    // Handle the MCP initialize method
                    if (method == "initialize")
                    {
                        _logger.LogInformation("Handling MCP initialize method");
                        
                        // Return server capabilities according to MCP protocol
                        var response = new JsonRpcResponse(id, result: new
                        {
                            protocolVersion = "2025-06-18",
                            serverInfo = new
                            {
                                name = "MCPInvoke",
                                version = "1.2.0"
                            },
                            capabilities = new
                            {
                                tools = new { }  // Indicates tool support
                            }
                        });
                        return JsonSerializer.Serialize(response, jsonOptions);
                    }

                    // Handle the notifications/initialized method
                    if (method == "notifications/initialized")
                    {
                        _logger.LogInformation("Handling MCP notifications/initialized method");
                        
                        // Acknowledge the initialization notification
                        var response = new JsonRpcResponse(id, result: new { success = true });
                        return JsonSerializer.Serialize(response, jsonOptions);
                    }

                    // Handle the tools/list method
                    if (method == "tools/list")
                    {
                        _logger.LogInformation("Handling MCP tools/list method");
                        
                        // Use the execution service to get tool definitions
                        var toolsListRequest = new JsonRpcRequest
                        {
                            JsonRpcVersion = "2.0",
                            Method = "tools/list",
                            Params = root.TryGetProperty("params", out var paramsElement) ? paramsElement.Clone() : null,
                            Id = id
                        };
                        
                        // Get tools from the execution service
                        var toolsResponse = await _executionService.ProcessRequestAsync(JsonSerializer.Serialize(toolsListRequest, jsonOptions));
                        return toolsResponse;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON-RPC request for special method handling");
            }

            // Not a special method
            return null;
        }
    }
}
