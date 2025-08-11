using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MCPInvoke.AspNetCore
{
    /// <summary>
    /// Implements <see cref="IMcpToolDefinitionProvider"/> by scanning ASP.NET Core controllers
    /// and exposing their action methods as MCP tools.
    /// </summary>
    public class AspNetControllerToolDefinitionProvider : IMcpToolDefinitionProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AspNetControllerToolDefinitionProvider> _logger;
        private readonly AspNetControllerToolOptions _options;
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly Dictionary<Type, object> _schemaCache = new Dictionary<Type, object>();
        private readonly HashSet<Type> _typesBeingProcessed = new HashSet<Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetControllerToolDefinitionProvider"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        /// <param name="logger">The logger instance for diagnostic output.</param>
        /// <param name="options">Configuration options for tool discovery.</param>
        /// <param name="assemblies">Assemblies to scan for controllers. If null, only the entry assembly is scanned.</param>
        public AspNetControllerToolDefinitionProvider(
            IServiceProvider serviceProvider,
            ILogger<AspNetControllerToolDefinitionProvider> logger,
            AspNetControllerToolOptions options,
            IEnumerable<Assembly>? assemblies = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new AspNetControllerToolOptions();
            _assemblies = assemblies ?? new[] { Assembly.GetEntryAssembly()! };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetControllerToolDefinitionProvider"/> class with default logging.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        /// <param name="options">Configuration options for tool discovery.</param>
        /// <param name="assemblies">Assemblies to scan for controllers. If null, only the entry assembly is scanned.</param>
        public AspNetControllerToolDefinitionProvider(
            IServiceProvider serviceProvider, 
            AspNetControllerToolOptions options,
            IEnumerable<Assembly>? assemblies = null)
            : this(serviceProvider, new Microsoft.Extensions.Logging.Abstractions.NullLogger<AspNetControllerToolDefinitionProvider>(), options, assemblies)
        {
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
            // Try to get description from attributes first
            var descriptionAttribute = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (descriptionAttribute != null && !string.IsNullOrEmpty(descriptionAttribute.Description))
            {
                return descriptionAttribute.Description;
            }
            
            // Try to get description from display name attribute
            var displayNameAttribute = method.GetCustomAttribute<DisplayNameAttribute>();
            if (displayNameAttribute != null && !string.IsNullOrEmpty(displayNameAttribute.DisplayName))
            {
                return displayNameAttribute.DisplayName;
            }
            
            // Default generic description
            return $"Action method {method.Name} from controller {method.DeclaringType?.Name ?? "Unknown"}";
        }

        private List<McpParameterInfo> GetInputSchema(MethodInfo method)
        {
            var parameters = new List<McpParameterInfo>();
            
            try
            {
                // 1. Extract route parameters first
                var routeParams = ExtractRouteParameters(method);
                foreach (var routeParam in routeParams)
                {
                    parameters.Add(new McpParameterInfo
                    {
                        Name = routeParam.Key,
                        Type = MapDotNetTypeToJsonSchemaType(routeParam.Value),
                        IsRequired = true,
                        Description = $"Route parameter {routeParam.Key}",
                        Source = "route",
                        Annotations = new Dictionary<string, object>
                        {
                            ["source"] = "route"
                        }
                    });
                }
                
                // 2. Process method parameters
                foreach (var param in method.GetParameters())
                {
                    // Skip if already handled as route parameter
                    if (routeParams.ContainsKey(param.Name ?? string.Empty))
                        continue;
                        
                    // Skip ASP.NET Core infrastructure types
                    if (IsAspNetCoreInfrastructureType(param.ParameterType))
                        continue;
                    
                    McpParameterInfo paramInfo;
                    
                    if (IsComplexType(param.ParameterType))
                    {
                        // Generate detailed schema for complex objects
                        paramInfo = GenerateComplexObjectSchema(param.ParameterType, param.Name ?? string.Empty);
                    }
                    else
                    {
                        // Handle primitive types, arrays, and enums
                        paramInfo = new McpParameterInfo
                        {
                            Name = param.Name ?? string.Empty,
                            Type = MapDotNetTypeToJsonSchemaType(param.ParameterType),
                            IsRequired = !param.IsOptional && !param.HasDefaultValue,
                            Description = GetParameterDescription(param)
                        };
                        
                        // Handle array types
                        if (IsArrayType(param.ParameterType))
                        {
                            var elementType = GetElementType(param.ParameterType);
                            paramInfo.Items = new McpParameterInfo
                            {
                                Name = "items",
                                Type = MapDotNetTypeToJsonSchemaType(elementType),
                                Description = $"Array item of type {elementType.Name}"
                            };
                            
                            // Also populate annotations for backward compatibility
                            paramInfo.Annotations = new Dictionary<string, object>
                            {
                                ["items"] = new Dictionary<string, object>
                                {
                                    ["type"] = MapDotNetTypeToJsonSchemaType(elementType)
                                }
                            };
                        }
                        // Handle enum types
                        else if (param.ParameterType.IsEnum)
                        {
                            var enumValues = Enum.GetNames(param.ParameterType).Cast<object>().ToList();
                            paramInfo.Enum = enumValues;
                            paramInfo.Annotations = new Dictionary<string, object>
                            {
                                ["enum"] = enumValues
                            };
                        }
                    }
                    
                    // Detect parameter source
                    var parameterSource = DetectParameterSource(param, method);
                    if (!string.IsNullOrEmpty(parameterSource))
                    {
                        paramInfo.Source = parameterSource;
                        paramInfo.Annotations ??= new Dictionary<string, object>();
                        paramInfo.Annotations["source"] = parameterSource;
                    }
                    
                    parameters.Add(paramInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating input schema for method {MethodName}", method.Name);
                throw;
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
        
        private Dictionary<string, Type> ExtractRouteParameters(MethodInfo method)
        {
            var routeParameters = new Dictionary<string, Type>();
            
            try
            {
                // Get route template from HttpMethodAttribute
                var httpAttribute = method.GetCustomAttributes()
                    .FirstOrDefault(a => a.GetType().Name.StartsWith("Http") && 
                                       a.GetType().Name.EndsWith("Attribute"));
                
                if (httpAttribute != null)
                {
                    var templateProperty = httpAttribute.GetType().GetProperty("Template");
                    if (templateProperty?.GetValue(httpAttribute) is string template)
                    {
                        // Parse route parameters like {stepDefinitionId}
                        var regex = new Regex(@"\{(\w+)(?::[^}]*)?\}");
                        var matches = regex.Matches(template);
                        
                        foreach (Match match in matches)
                        {
                            var paramName = match.Groups[1].Value;
                            
                            // Find corresponding method parameter
                            var methodParam = method.GetParameters()
                                .FirstOrDefault(p => p.Name == paramName);
                            
                            if (methodParam != null)
                            {
                                routeParameters[paramName] = methodParam.ParameterType;
                            }
                        }
                    }
                }
                
                // Also check controller-level route
                var controllerRoute = method.DeclaringType?.GetCustomAttribute<RouteAttribute>();
                if (controllerRoute?.Template != null)
                {
                    var regex = new Regex(@"\{(\w+)(?::[^}]*)?\}");
                    var matches = regex.Matches(controllerRoute.Template);
                    
                    foreach (Match match in matches)
                    {
                        var paramName = match.Groups[1].Value;
                        var methodParam = method.GetParameters()
                            .FirstOrDefault(p => p.Name == paramName);
                        
                        if (methodParam != null && !routeParameters.ContainsKey(paramName))
                        {
                            routeParameters[paramName] = methodParam.ParameterType;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting route parameters for method {MethodName}", method.Name);
            }
            
            return routeParameters;
        }
        
        private McpParameterInfo GenerateComplexObjectSchema(Type objectType, string paramName)
        {
            var objectProperties = GenerateObjectProperties(objectType);
            var requiredProps = GetRequiredProperties(objectType);
            
            var schema = new McpParameterInfo
            {
                Name = paramName,
                Type = "object",
                IsRequired = true,
                Description = $"Complex object of type {objectType.Name}",
                Properties = ConvertObjectPropertiesToMcpParameters(objectProperties),
                Required = requiredProps,
                Annotations = new Dictionary<string, object>
                {
                    ["properties"] = objectProperties,
                    ["required"] = requiredProps
                }
            };
            
            return schema;
        }
        
        private Dictionary<string, McpParameterInfo> ConvertObjectPropertiesToMcpParameters(Dictionary<string, object> objectProperties)
        {
            var mcpProperties = new Dictionary<string, McpParameterInfo>();
            
            foreach (var prop in objectProperties)
            {
                if (prop.Value is Dictionary<string, object> propDef)
                {
                    var mcpParam = new McpParameterInfo
                    {
                        Name = prop.Key,
                        Type = propDef.TryGetValue("type", out var typeObj) ? typeObj?.ToString() ?? "string" : "string",
                        Description = propDef.TryGetValue("description", out var descObj) ? descObj?.ToString() ?? "" : ""
                    };
                    
                    // Handle nested object properties
                    if (mcpParam.Type == "object" && propDef.TryGetValue("properties", out var nestedPropsObj) 
                        && nestedPropsObj is Dictionary<string, object> nestedProps)
                    {
                        mcpParam.Properties = ConvertObjectPropertiesToMcpParameters(nestedProps);
                    }
                    
                    // Handle array items
                    if (mcpParam.Type == "array" && propDef.TryGetValue("items", out var itemsObj))
                    {
                        if (itemsObj is Dictionary<string, object> itemsDef)
                        {
                            mcpParam.Items = new McpParameterInfo
                            {
                                Name = "items",
                                Type = itemsDef.TryGetValue("type", out var itemTypeObj) ? itemTypeObj?.ToString() ?? "string" : "string",
                                Description = itemsDef.TryGetValue("description", out var itemDescObj) ? itemDescObj?.ToString() ?? "" : ""
                            };
                        }
                    }
                    
                    // Handle enum values
                    if (propDef.TryGetValue("enum", out var enumObj) && enumObj is List<object> enumValues)
                    {
                        mcpParam.Enum = enumValues;
                    }
                    
                    mcpProperties[prop.Key] = mcpParam;
                }
            }
            
            return mcpProperties;
        }
        
        private Dictionary<string, object> GenerateObjectProperties(Type type)
        {
            var properties = new Dictionary<string, object>();
            
            // Check for circular reference
            if (_typesBeingProcessed.Contains(type))
            {
                // Return placeholder for circular references
                return new Dictionary<string, object>
                {
                    ["_circular_reference"] = new { type = "object", description = $"Circular reference to {type.Name}" }
                };
            }
            
            // Cache check
            if (_schemaCache.TryGetValue(type, out var cached))
            {
                if (cached is Dictionary<string, object> cachedProps)
                    return cachedProps;
            }
            
            _typesBeingProcessed.Add(type);
            
            try
            {
                // Walk the full inheritance chain to include base class properties
                // This fixes the issue where inherited properties (like Provider, ModelName, PromptVersion) were missing
                var classProperties = GetInheritanceChainProperties(type);
                
                foreach (var prop in classProperties)
                {
                    if (IsComplexType(prop.PropertyType))
                    {
                        properties[prop.Name] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = GenerateObjectProperties(prop.PropertyType),
                            ["description"] = GetPropertyDescription(prop)
                        };
                    }
                    else if (IsArrayType(prop.PropertyType))
                    {
                        var elementType = GetElementType(prop.PropertyType);
                        properties[prop.Name] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = MapDotNetTypeToJsonSchemaType(elementType)
                            },
                            ["description"] = GetPropertyDescription(prop)
                        };
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        properties[prop.Name] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["enum"] = Enum.GetNames(prop.PropertyType).Cast<object>().ToList(),
                            ["description"] = GetPropertyDescription(prop)
                        };
                    }
                    else
                    {
                        properties[prop.Name] = new Dictionary<string, object>
                        {
                            ["type"] = MapDotNetTypeToJsonSchemaType(prop.PropertyType),
                            ["description"] = GetPropertyDescription(prop)
                        };
                    }
                }
                
                // Cache the result
                _schemaCache[type] = properties;
            }
            finally
            {
                _typesBeingProcessed.Remove(type);
            }
            
            return properties;
        }
        
        private List<string> GetRequiredProperties(Type type)
        {
            var required = new List<string>();
            
            // Walk the inheritance chain to include base class properties for Required attribute detection
            var properties = GetInheritanceChainProperties(type);
            
            foreach (var prop in properties)
            {
                // Check for Required attribute
                if (prop.GetCustomAttribute<RequiredAttribute>() != null)
                {
                    required.Add(prop.Name);
                }
                // Non-nullable reference types in nullable context could also be considered required
                // This is a simplified check - more sophisticated nullable analysis could be added
                else if (!IsNullableType(prop.PropertyType) && !prop.PropertyType.IsValueType)
                {
                    // For now, don't auto-add reference types as required to be conservative
                    // required.Add(prop.Name);
                }
            }
            
            return required;
        }
        
        private string DetectParameterSource(ParameterInfo param, MethodInfo method)
        {
            // Check for ASP.NET Core binding attributes
            if (param.GetCustomAttribute<FromRouteAttribute>() != null)
                return "route";
            if (param.GetCustomAttribute<FromBodyAttribute>() != null) 
                return "body";
            if (param.GetCustomAttribute<FromQueryAttribute>() != null)
                return "query";
            if (param.GetCustomAttribute<FromHeaderAttribute>() != null)
                return "header";
            if (param.GetCustomAttribute<FromFormAttribute>() != null)
                return "form";
            
            // Default inference based on type and HTTP method
            if (IsComplexType(param.ParameterType))
                return "body";
            
            // Check HTTP method to infer parameter source
            var httpMethod = GetHttpMethod(method);
            if (httpMethod == "GET" || httpMethod == "DELETE")
                return "query";
            
            return "query"; // Default fallback
        }
        
        private string GetHttpMethod(MethodInfo method)
        {
            var httpAttribute = method.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name.StartsWith("Http") && 
                               a.GetType().Name.EndsWith("Attribute"));
            
            if (httpAttribute != null)
            {
                var typeName = httpAttribute.GetType().Name;
                if (typeName.StartsWith("Http") && typeName.EndsWith("Attribute"))
                {
                    return typeName.Substring(4, typeName.Length - 13).ToUpper();
                }
            }
            
            return "GET"; // Default
        }
        
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
            
            // Arrays and collections are not complex objects (they're arrays)
            if (IsArrayType(type))
            {
                return false;
            }
            
            // Everything else is complex
            return underlyingType.IsClass || underlyingType.IsInterface || underlyingType.IsValueType;
        }
        
        private bool IsArrayType(Type type)
        {
            return type.IsArray || 
                   (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type));
        }
        
        private Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType() ?? typeof(object);
            }
            else if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            {
                return type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            }
            
            return typeof(object);
        }
        
        private bool IsNullableType(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }
        
        private string GetPropertyDescription(PropertyInfo property)
        {
            // Try to get description from attributes
            var displayAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
            if (displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.DisplayName))
            {
                return displayAttribute.DisplayName;
            }
            
            var descriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttribute != null && !string.IsNullOrEmpty(descriptionAttribute.Description))
            {
                return descriptionAttribute.Description;
            }
            
            // Default description
            return $"Property {property.Name} of type {property.PropertyType.Name}";
        }
        
        private string MapDotNetTypeToJsonSchemaType(Type dotNetType)
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(dotNetType) ?? dotNetType;
            
            // Map .NET types to JSON Schema types
            if (underlyingType == typeof(string) || underlyingType == typeof(char) || underlyingType == typeof(Guid))
                return "string";
            else if (underlyingType == typeof(bool))
                return "boolean";
            else if (underlyingType == typeof(byte) || underlyingType == typeof(sbyte) || 
                     underlyingType == typeof(short) || underlyingType == typeof(ushort) ||
                     underlyingType == typeof(int) || underlyingType == typeof(uint) ||
                     underlyingType == typeof(long) || underlyingType == typeof(ulong))
                return "integer";
            else if (underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal))
                return "number";
            else if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset) || underlyingType == typeof(TimeSpan))
                return "string"; // With format: date-time
            else if (underlyingType.IsEnum)
                return "string"; // Enums as strings
            else if (IsArrayType(underlyingType))
                return "array";
            else if (underlyingType != typeof(string) && (underlyingType.IsClass || underlyingType.IsInterface))
                return "object";
            
            // Default to string for anything else
            return "string";
        }
        
        private string GetParameterDescription(ParameterInfo param)
        {
            // Placeholder for more advanced parameter description extraction
            // Could be enhanced to read XML comments or custom attributes
            return $"Parameter {param.Name} of type {param.ParameterType.Name}";
        }
        
        /// <summary>
        /// Gets properties from the entire inheritance chain of a type.
        /// This ensures that base class properties are included in schema generation.
        /// </summary>
        private PropertyInfo[] GetInheritanceChainProperties(Type type)
        {
            var properties = new List<PropertyInfo>();
            var currentType = type;

            // Walk up inheritance chain - essential for PromptRequest : LlmProviderModelRequest
            while (currentType != null && currentType != typeof(object))
            {
                var declaredProperties = currentType.GetProperties(
                    BindingFlags.Public | 
                    BindingFlags.Instance | 
                    BindingFlags.DeclaredOnly)
                    .Where(p => p.CanRead);
                
                properties.AddRange(declaredProperties);
                currentType = currentType.BaseType;
            }

            return properties.ToArray();
        }
    }
}
