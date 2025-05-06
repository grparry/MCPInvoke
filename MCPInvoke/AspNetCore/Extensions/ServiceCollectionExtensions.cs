using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MCPInvoke.Extensions;

namespace MCPInvoke.AspNetCore.Extensions
{
    /// <summary>
    /// Extension methods for setting up MCP services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds MCP Invoke services with ASP.NET Core controller integration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddMcpInvokeWithControllers(this IServiceCollection services)
        {
            return AddMcpInvokeWithControllers(services, options => { }, assemblies: null!);
        }

        /// <summary>
        /// Adds MCP Invoke services with ASP.NET Core controller integration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="AspNetControllerToolOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddMcpInvokeWithControllers(
            this IServiceCollection services,
            Action<AspNetControllerToolOptions> configureOptions)
        {
            return AddMcpInvokeWithControllers(services, configureOptions, assemblies: null!);
        }

        /// <summary>
        /// Adds MCP Invoke services with ASP.NET Core controller integration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="assemblies">Assemblies to scan for controllers.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddMcpInvokeWithControllers(
            this IServiceCollection services,
            IEnumerable<Assembly> assemblies)
        {
            return AddMcpInvokeWithControllers(services, options => { }, assemblies);
        }

        /// <summary>
        /// Adds MCP Invoke services with ASP.NET Core controller integration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="AspNetControllerToolOptions"/>.</param>
        /// <param name="assemblies">Assemblies to scan for controllers.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddMcpInvokeWithControllers(
            this IServiceCollection services,
            Action<AspNetControllerToolOptions> configureOptions,
            IEnumerable<Assembly> assemblies)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configureOptions == null)
            {
                throw new ArgumentNullException(nameof(configureOptions));
            }

            // Register standard MCPInvoke services first
            services.AddMcpInvoke();

            // Register the assemblies to scan if provided
            if (assemblies != null)
            {
                services.AddSingleton(assemblies);
            }

            // Configure options
            var options = new AspNetControllerToolOptions();
            configureOptions(options);
            services.AddSingleton(options);

            // Register the controller tool definition provider
            services.AddSingleton<IMcpToolDefinitionProvider, AspNetControllerToolDefinitionProvider>();

            return services;
        }
    }
}
