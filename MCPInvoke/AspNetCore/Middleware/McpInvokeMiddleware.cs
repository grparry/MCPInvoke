using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
                    // Process the request using the execution service
                    string responseText = await _executionService.ProcessRequestAsync(requestBody);
                    
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
    }
}
