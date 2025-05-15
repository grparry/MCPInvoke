using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MCPInvoke.AspNetCore.Middleware;

namespace MCPInvoke.AspNetCore.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to add MCP Invoke middleware to the request pipeline.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the MCP Invoke middleware to the request pipeline for the specified path.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <param name="path">The path to serve MCP Invoke requests from. Defaults to "/mcpinvoke".</param>
        /// <returns>The <see cref="IApplicationBuilder"/> so that additional calls can be chained.</returns>
        public static IApplicationBuilder UseMcpInvoke(this IApplicationBuilder app, string path = "/mcpinvoke")
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            // Remove any trailing slash from the path
            if (path.EndsWith("/"))
            {
                path = path.TrimEnd('/');
            }

            // Add the middleware for the specific path
            app.Map(path, branch =>
            {
                branch.UseMiddleware<McpInvokeMiddleware>();
            });

            return app;
        }
    }
}
