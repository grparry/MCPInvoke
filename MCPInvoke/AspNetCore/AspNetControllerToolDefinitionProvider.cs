using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MCPInvoke.AspNetCore
{
    /// <summary>
    /// Implements <see cref="IMcpToolDefinitionProvider"/> by scanning ASP.NET Core controllers
    /// and exposing their action methods as MCP tools.
    /// </summary>
    public class AspNetControllerToolDefinitionProvider : IMcpToolDefinitionProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AspNetControllerToolOptions _options;
        private readonly IEnumerable<Assembly> _assemblies;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetControllerToolDefinitionProvider"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        /// <param name="options">Configuration options for tool discovery.</param>
        /// <param name="assemblies">Assemblies to scan for controllers. If null, only the entry assembly is scanned.</param>
        public AspNetControllerToolDefinitionProvider(
            IServiceProvider serviceProvider, 
            AspNetControllerToolOptions options,
            IEnumerable<Assembly>? assemblies = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? new AspNetControllerToolOptions();
            _assemblies = assemblies ?? new[] { Assembly.GetEntryAssembly()! };
        }

        /// <inheritdoc />
        public IEnumerable<McpToolDefinition> GetToolDefinitions()
        {
            var tools = new List<McpToolDefinition>();

            foreach (var assembly in _assemblies.Where(a => a != null))
            {
                // Find controller types in the assembly
                var controllerTypes = assembly.GetTypes()
                    .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && 
                               !t.IsAbstract && 
                               !IsExcluded(t))
                    .ToList();

                foreach (var controllerType in controllerTypes)
                {
                    var controllerName = GetControllerName(controllerType);
                    
                    // Get public methods with HTTP verb attributes
                    var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Where(m => !m.IsSpecialName && IsActionMethod(m))
                        .ToList();

                    foreach (var method in methods)
                    {
                        var toolName = BuildToolName(controllerName, method.Name);
                        
                        var toolDefinition = new McpToolDefinition
                        {
                            Name = toolName,
                            HandlerTypeAssemblyQualifiedName = controllerType.AssemblyQualifiedName ?? $"{controllerType.FullName}, {controllerType.Assembly.GetName().Name}",
                            MethodName = method.Name,
                            InputSchema = GetInputSchema(method),
                            Description = GetMethodDescription(method)
                        };

                        tools.Add(toolDefinition);
                    }
                }
            }

            return tools;
        }

        private bool IsExcluded(Type controllerType)
        {
            if (_options.ExcludedControllers == null || !_options.ExcludedControllers.Any())
                return false;
                
            var controllerName = controllerType.Name.Replace("Controller", "");
            return _options.ExcludedControllers.Contains(controllerName, StringComparer.OrdinalIgnoreCase);
        }

        private string GetControllerName(Type controllerType)
        {
            // Default ASP.NET Core convention is to remove "Controller" suffix
            var name = controllerType.Name;
            if (name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "Controller".Length);
            }
            
            return name;
        }

        private bool IsActionMethod(MethodInfo method)
        {
            // Look for HTTP verb attributes
            var httpAttributes = method.GetCustomAttributes()
                .Where(a => a.GetType().Name.StartsWith("Http") && 
                          a.GetType().Name.EndsWith("Attribute"))
                .Any();
                
            return httpAttributes;
        }

        private string BuildToolName(string controllerName, string methodName)
        {
            if (_options.IncludeControllerNameInToolName)
            {
                return $"{controllerName}_{methodName}";
            }
            
            return methodName;
        }
        
        private string GetMethodDescription(MethodInfo method)
        {
            // Try to get description from XML comments or attributes
            // This is a placeholder for more advanced description extraction
            return $"Action method {method.Name} from controller {method.DeclaringType?.Name ?? "Unknown"}";
        }

        private List<McpParameterInfo> GetInputSchema(MethodInfo method)
        {
            var parameters = new List<McpParameterInfo>();
            
            foreach (var param in method.GetParameters())
            {
                // Skip parameters that are likely ASP.NET Core infrastructure parameters
                if (IsAspNetCoreInfrastructureType(param.ParameterType))
                {
                    continue;
                }
                
                var paramType = param.ParameterType;
                var mcpType = MapDotNetTypeToMcpType(paramType);
                
                parameters.Add(new McpParameterInfo
                {
                    Name = param.Name ?? string.Empty,
                    Type = mcpType,
                    IsRequired = !param.IsOptional,
                    Description = GetParameterDescription(param)
                });
            }
            
            return parameters;
        }
        
        private bool IsAspNetCoreInfrastructureType(Type type)
        {
            // Skip common ASP.NET Core infrastructure types that shouldn't be exposed to MCP
            var infraTypes = new[]
            {
                "Microsoft.AspNetCore.Http.HttpContext",
                "System.Threading.CancellationToken",
                "Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary",
                "Microsoft.AspNetCore.Mvc.ActionContext"
            };
            
            return infraTypes.Contains(type.FullName);
        }
        
        private string MapDotNetTypeToMcpType(Type dotNetType)
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(dotNetType) ?? dotNetType;
            
            // Map .NET types to JSON Schema types
            if (underlyingType == typeof(int) || 
                underlyingType == typeof(long) || 
                underlyingType == typeof(short) || 
                underlyingType == typeof(byte) ||
                underlyingType == typeof(uint) ||
                underlyingType == typeof(ulong) ||
                underlyingType == typeof(ushort) ||
                underlyingType == typeof(sbyte))
            {
                return "integer";
            }
            else if (underlyingType == typeof(double) || 
                     underlyingType == typeof(float) || 
                     underlyingType == typeof(decimal))
            {
                return "number";
            }
            else if (underlyingType == typeof(bool))
            {
                return "boolean";
            }
            else if (underlyingType == typeof(DateTime) || 
                     underlyingType == typeof(DateTimeOffset) ||
                     underlyingType == typeof(TimeSpan))
            {
                return "string"; // Use string format for dates
            }
            else if (underlyingType.IsEnum)
            {
                return "string"; // Use string for enums
            }
            else if (underlyingType == typeof(Guid))
            {
                return "string";
            }
            else if (underlyingType.IsArray || 
                     (underlyingType.IsGenericType && 
                      typeof(IEnumerable<>).IsAssignableFrom(underlyingType.GetGenericTypeDefinition())))
            {
                return "array";
            }
            else if (underlyingType != typeof(string) && 
                     !underlyingType.IsPrimitive)
            {
                return "object"; // Complex types
            }
            
            // Default to string for anything else
            return "string";
        }
        
        private string GetParameterDescription(ParameterInfo param)
        {
            // Placeholder for more advanced parameter description extraction
            // Could be enhanced to read XML comments or custom attributes
            return $"Parameter {param.Name} of type {param.ParameterType.Name}";
        }
    }
}
