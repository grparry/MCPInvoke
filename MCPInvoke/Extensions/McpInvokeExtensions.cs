using MCPInvoke.Services;
using MCPInvoke.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using System;

namespace MCPInvoke.Extensions;

/// <summary>
/// Provides extension methods for setting up and mapping MCPInvoke services and endpoints.
/// </summary>
public static class McpInvokeExtensions
{
    /// <summary>
    /// Adds MCPInvoke services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMcpInvoke(this IServiceCollection services)
    {
        services.AddSingleton<McpExecutionService>();
        return services;
    }

    /// <summary>
    /// Adds an MCP execution endpoint to the <see cref="IEndpointRouteBuilder" />.
    /// This version is designed to be more compatible with Minimal API conventions and OpenAPI integration.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" /> to add the endpoint to.</param>
    /// <param name="pattern">The route pattern for the MCP endpoint. Defaults to "/mcp".</param>
    /// <returns>A <see cref="RouteHandlerBuilder" /> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder MapMcpInvoke(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        // Define the handler with DI for McpExecutionService and direct HttpRequest access.
        // This signature allows MapPost to return a RouteHandlerBuilder.
        async Task<IResult> McpInvokeHandler(HttpRequest httpRequest, McpExecutionService mcpService)
        {
            string requestBody;
            using (var reader = new StreamReader(httpRequest.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // Parse the request to check for special JSON-RPC methods
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

                    // Handle special JSON-RPC methods
                    if (method == "notifications/initialized")
                    {
                        // Acknowledge the initialization notification
                        var response = new JsonRpcResponse(id, result: new { success = true });
                        var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                        return Results.Text(responseJson, contentType: "application/json-rpc+json; charset=utf-8");
                    }

                    // For the tools/list method, let the MCPBuckle handle it
                    if (method == "tools/list")
                    {
                        var error = new JsonRpcError { Code = -32601, Message = $"Method '{method}' not supported at this endpoint. Use the MCP context endpoint instead." };
                        var response = new JsonRpcResponse(id, error: error);
                        var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                        return Results.Text(responseJson, contentType: "application/json-rpc+json; charset=utf-8");
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, continue to standard processing
            }

            // Standard processing for tool execution
            var responseBody = await mcpService.ProcessRequestAsync(requestBody);
            
            // Use Results.Text for proper IResult handling compatible with RouteHandlerBuilder
            return Results.Text(responseBody, contentType: "application/json-rpc+json; charset=utf-8");
        }

        // This call to MapPost, with the McpInvokeHandler delegate, should return RouteHandlerBuilder.
        return endpoints.MapPost(pattern, McpInvokeHandler);
    }
}
