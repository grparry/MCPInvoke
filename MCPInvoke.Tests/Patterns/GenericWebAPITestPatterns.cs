using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPBuckle.Configuration;
using MCPBuckle.Models;
using MCPBuckle.Services;
using MCPInvoke.Models;
using MCPInvoke.Services;

namespace MCPInvoke.Tests.Patterns
{
    /// <summary>
    /// Generic web API test patterns for common business domains.
    /// These patterns can be adapted for any REST API endpoint testing.
    /// 
    /// Proven with MCPInvoke v2.0 Enhanced Parameter Binding (128/128 tests passing)
    /// and MCPBuckle v2.0 Enhanced Parameter Source Detection (125/125 tests passing).
    /// </summary>
    public static class GenericWebAPITestPatterns
    {
        #region Core Pattern Templates

        /// <summary>
        /// Pattern for nested resource route structure: /api/organizations/{orgId}/users/{userId}/[resource]
        /// Common in endpoints like UserProjects, UserOrders, UserPreferences, etc.
        /// </summary>
        public class NestedResourceRoutePattern
        {
            public static ControllerActionDescriptor CreateActionDescriptor(
                string controllerName,
                string actionName,
                Type controllerType,
                string resourcePath,
                List<ParameterDescriptor> additionalParameters = null)
            {
                var baseParameters = new List<ParameterDescriptor>
                {
                    new ControllerParameterDescriptor { Name = "orgId", ParameterType = typeof(int) },
                    new ControllerParameterDescriptor { Name = "userId", ParameterType = typeof(int) }
                };

                if (additionalParameters != null)
                    baseParameters.AddRange(additionalParameters);

                var methodInfo = controllerType.GetMethod(actionName);
                return new ControllerActionDescriptor
                {
                    ControllerName = controllerName,
                    ActionName = actionName,
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo,
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = $"api/organizations/{{orgId}}/users/{{userId}}/{resourcePath}"
                    },
                    Parameters = baseParameters
                };
            }

            public static async Task<object[]> TestSuccessfulBinding(
                EnhancedParameterBindingService bindingService,
                MethodInfo methodInfo,
                List<McpParameterInfo> schema,
                int orgId = 123,
                int userId = 456,
                Dictionary<string, object> additionalParams = null)
            {
                var jsonParams = new Dictionary<string, object>
                {
                    { "orgId", orgId },
                    { "userId", userId }
                };

                if (additionalParams != null)
                {
                    foreach (var kvp in additionalParams)
                        jsonParams[kvp.Key] = kvp.Value;
                }

                var jsonString = JsonSerializer.Serialize(jsonParams);
                var paramsJson = JsonDocument.Parse(jsonString).RootElement;

                return await bindingService.BindParametersAsync(
                    methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);
            }

            public static void AssertRouteParametersCorrect(object[] boundParameters, int expectedOrgId = 123, int expectedUserId = 456)
            {
                Assert.NotNull(boundParameters);
                Assert.True(boundParameters.Length >= 2);
                Assert.Equal(expectedOrgId, boundParameters[0]); // orgId
                Assert.Equal(expectedUserId, boundParameters[1]); // userId
            }
        }

        /// <summary>
        /// Pattern for organization-scoped route structure: /api/organizations/{orgId}/[resource]
        /// Common in endpoints like Users, Projects, Orders, Categories, etc.
        /// </summary>
        public class OrganizationScopedRoutePattern
        {
            public static ControllerActionDescriptor CreateActionDescriptor(
                string controllerName,
                string actionName,
                Type controllerType,
                string resourcePath,
                List<ParameterDescriptor> additionalParameters = null)
            {
                var baseParameters = new List<ParameterDescriptor>
                {
                    new ControllerParameterDescriptor { Name = "orgId", ParameterType = typeof(int) }
                };

                if (additionalParameters != null)
                    baseParameters.AddRange(additionalParameters);

                var methodInfo = controllerType.GetMethod(actionName);
                return new ControllerActionDescriptor
                {
                    ControllerName = controllerName,
                    ActionName = actionName,
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo,
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = $"api/organizations/{{orgId}}/{resourcePath}"
                    },
                    Parameters = baseParameters
                };
            }
        }

        /// <summary>
        /// Pattern for resource-specific routes: /api/[resource]/{id}
        /// Common in endpoints like Products, Categories, Orders, Posts, etc.
        /// </summary>
        public class ResourceIdRoutePattern
        {
            public static ControllerActionDescriptor CreateActionDescriptor(
                string controllerName,
                string actionName,
                Type controllerType,
                string resourceName,
                string idParameterName = "id",
                Type idParameterType = null,
                List<ParameterDescriptor> additionalParameters = null)
            {
                idParameterType ??= typeof(int);

                var baseParameters = new List<ParameterDescriptor>
                {
                    new ControllerParameterDescriptor { Name = idParameterName, ParameterType = idParameterType }
                };

                if (additionalParameters != null)
                    baseParameters.AddRange(additionalParameters);

                var methodInfo = controllerType.GetMethod(actionName);
                return new ControllerActionDescriptor
                {
                    ControllerName = controllerName,
                    ActionName = actionName,
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo,
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = $"api/{resourceName}/{{{idParameterName}}}"
                    },
                    Parameters = baseParameters
                };
            }
        }

        #endregion

        #region Request/Response Pattern Templates

        /// <summary>
        /// Pattern for complex request models with DTOs
        /// Common in POST/PUT endpoints with CreateProductDto, UpdateUserDto, OrderRequest, etc.
        /// </summary>
        public class ComplexRequestPattern
        {
            public static List<McpParameterInfo> CreateSchemaWithComplexRequest(
                string requestParameterName,
                Type requestType,
                Dictionary<string, McpParameterInfo> requestProperties,
                List<string> requiredProperties = null)
            {
                return new List<McpParameterInfo>
                {
                    new McpParameterInfo
                    {
                        Name = requestParameterName,
                        Type = "object",
                        Source = "body",
                        IsRequired = true,
                        Properties = requestProperties,
                        Required = requiredProperties ?? new List<string>(),
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "http_method_inference" },
                            { "httpMethod", "POST" },
                            { "validationRules", "complex_object_validation" }
                        }
                    }
                };
            }

            public static async Task<T> TestComplexObjectBinding<T>(
                EnhancedParameterBindingService bindingService,
                MethodInfo methodInfo,
                List<McpParameterInfo> schema,
                object requestObject,
                string requestParameterName = "request")
            {
                var jsonParams = new Dictionary<string, object>
                {
                    { requestParameterName, requestObject }
                };

                var jsonString = JsonSerializer.Serialize(jsonParams);
                var paramsJson = JsonDocument.Parse(jsonString).RootElement;

                var boundParameters = await bindingService.BindParametersAsync(
                    methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);

                Assert.NotNull(boundParameters);
                Assert.Single(boundParameters);
                return Assert.IsType<T>(boundParameters[0]);
            }
        }

        /// <summary>
        /// Pattern for pagination parameters
        /// Common in GET endpoints with pageNumber, pageSize, sortBy, sortOrder
        /// </summary>
        public class PaginationPattern
        {
            public static List<McpParameterInfo> CreatePaginationSchema(
                int defaultPageSize = 20,
                string defaultSortBy = "Id",
                string defaultSortOrder = "Ascending")
            {
                return new List<McpParameterInfo>
                {
                    new McpParameterInfo
                    {
                        Name = "pageNumber",
                        Type = "integer",
                        Source = "query",
                        IsRequired = false,
                        Default = 1,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "http_method_inference" },
                            { "httpMethod", "GET" }
                        }
                    },
                    new McpParameterInfo
                    {
                        Name = "pageSize",
                        Type = "integer",
                        Source = "query",
                        IsRequired = false,
                        Default = defaultPageSize,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "http_method_inference" },
                            { "httpMethod", "GET" }
                        }
                    },
                    new McpParameterInfo
                    {
                        Name = "sortBy",
                        Type = "string",
                        Source = "query",
                        IsRequired = false,
                        Default = defaultSortBy,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "http_method_inference" },
                            { "httpMethod", "GET" }
                        }
                    },
                    new McpParameterInfo
                    {
                        Name = "sortOrder",
                        Type = "string",
                        Source = "query",
                        IsRequired = false,
                        Default = defaultSortOrder,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "http_method_inference" },
                            { "httpMethod", "GET" }
                        }
                    }
                };
            }

            public static void AssertPaginationDefaults(object[] boundParameters, int startIndex = 2)
            {
                Assert.True(boundParameters.Length >= startIndex + 4);
                Assert.IsType<int>(boundParameters[startIndex]); // pageNumber
                Assert.IsType<int>(boundParameters[startIndex + 1]); // pageSize  
                Assert.IsType<string>(boundParameters[startIndex + 2]); // sortBy
                Assert.IsType<string>(boundParameters[startIndex + 3]); // sortOrder
            }
        }

        /// <summary>
        /// Pattern for enum parameters
        /// Common in endpoints with status, category, priority enums, etc.
        /// </summary>
        public class EnumParameterPattern
        {
            public static McpParameterInfo CreateEnumParameter<T>(
                string parameterName,
                string source = "query",
                bool isRequired = false,
                object defaultValue = null) where T : Enum
            {
                return new McpParameterInfo
                {
                    Name = parameterName,
                    Type = "string",
                    Source = source,
                    IsRequired = isRequired,
                    Default = defaultValue,
                    Format = "enum",
                    Enum = Enum.GetValues(typeof(T)).Cast<object>().ToList(),
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", source == "body" ? "http_method_inference" : "http_method_inference" },
                        { "IsStringEnum", true }
                    }
                };
            }

            public static async Task TestEnumBinding<T>(
                EnhancedParameterBindingService bindingService,
                MethodInfo methodInfo,
                List<McpParameterInfo> schema,
                string enumValue,
                T expectedEnum,
                string parameterName = "status") where T : Enum
            {
                var jsonParams = new Dictionary<string, object>
                {
                    { parameterName, enumValue }
                };

                var jsonString = JsonSerializer.Serialize(jsonParams);
                var paramsJson = JsonDocument.Parse(jsonString).RootElement;

                var boundParameters = await bindingService.BindParametersAsync(
                    methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);

                Assert.NotNull(boundParameters);
                Assert.Equal(expectedEnum, boundParameters[0]);
            }
        }

        #endregion

        #region Business Workflow Patterns

        /// <summary>
        /// Pattern for order processing and business workflow endpoints
        /// Common in order execution, project management, task workflow systems
        /// </summary>
        public class BusinessWorkflowPattern
        {
            public static List<McpParameterInfo> CreateWorkflowExecutionSchema(bool includeExecutionId = false)
            {
                var schema = new List<McpParameterInfo>
                {
                    new McpParameterInfo
                    {
                        Name = "request",
                        Type = "object",
                        Source = "body",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "http_method_inference" },
                            { "httpMethod", "POST" }
                        }
                    }
                };

                if (includeExecutionId)
                {
                    schema.Add(new McpParameterInfo
                    {
                        Name = "executionId",
                        Type = "integer",
                        Source = "route",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "route_template_analysis" },
                            { "routeTemplate", "api/orders/execution/{executionId}" }
                        }
                    });
                }

                return schema;
            }
        }

        /// <summary>
        /// Pattern for user preferences and configuration management
        /// Common in settings, preferences, theme endpoints with user/org/provider combinations
        /// </summary>
        public class PreferenceManagementPattern
        {
            public static List<McpParameterInfo> CreatePreferenceSchema(
                bool includeOrgId = true,
                bool includeCategory = true,
                bool includeProvider = true)
            {
                var schema = new List<McpParameterInfo>();

                if (includeOrgId)
                {
                    schema.Add(new McpParameterInfo
                    {
                        Name = "orgId",
                        Type = "integer",
                        Source = "route",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "route_template_analysis" }
                        }
                    });
                }

                if (includeCategory)
                {
                    schema.Add(new McpParameterInfo
                    {
                        Name = "category",
                        Type = "string",
                        Source = "route",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "route_template_analysis" }
                        }
                    });
                }

                if (includeProvider)
                {
                    schema.Add(new McpParameterInfo
                    {
                        Name = "provider",
                        Type = "string",
                        Source = "route",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            { "sourceDetectionMethod", "route_template_analysis" }
                        }
                    });
                }

                return schema;
            }
        }

        #endregion

        #region Error Handling and Edge Case Patterns

        /// <summary>
        /// Pattern for testing missing required parameters
        /// Essential for all REST API endpoints to ensure proper error handling
        /// </summary>
        public class ErrorHandlingPattern
        {
            public static async Task TestMissingRequiredParameter(
                EnhancedParameterBindingService bindingService,
                MethodInfo methodInfo,
                List<McpParameterInfo> schema,
                string missingParameterName,
                Dictionary<string, object> providedParams = null)
            {
                providedParams ??= new Dictionary<string, object>();
                
                // Ensure the missing parameter is not included
                providedParams.Remove(missingParameterName);

                var jsonString = JsonSerializer.Serialize(providedParams);
                var paramsJson = JsonDocument.Parse(jsonString).RootElement;

                var boundParameters = await bindingService.BindParametersAsync(
                    methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);

                // Should return null indicating binding failure for missing required parameter
                Assert.Null(boundParameters);
            }

            public static async Task TestOptionalParameterDefaults(
                EnhancedParameterBindingService bindingService,
                MethodInfo methodInfo,
                List<McpParameterInfo> schema,
                Dictionary<string, object> requiredParams,
                Dictionary<string, object> expectedDefaults)
            {
                var jsonString = JsonSerializer.Serialize(requiredParams);
                var paramsJson = JsonDocument.Parse(jsonString).RootElement;

                var boundParameters = await bindingService.BindParametersAsync(
                    methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);

                Assert.NotNull(boundParameters);

                // Verify that default values were applied correctly
                var parameters = methodInfo.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    if (expectedDefaults.ContainsKey(param.Name))
                    {
                        Assert.Equal(expectedDefaults[param.Name], boundParameters[i]);
                    }
                }
            }
        }

        #endregion

        #region Performance Testing Patterns

        /// <summary>
        /// Pattern for performance testing with large parameter sets
        /// Based on proven MCPInvoke v2.0 performance test (< 100ms for 15 parameters)
        /// </summary>
        public class PerformanceTestPattern
        {
            public static async Task TestPerformanceWithinBounds(
                EnhancedParameterBindingService bindingService,
                MethodInfo methodInfo,
                List<McpParameterInfo> schema,
                Dictionary<string, object> parameters,
                int expectedParameterCount,
                int maxMilliseconds = 100)
            {
                var jsonString = JsonSerializer.Serialize(parameters);
                var paramsJson = JsonDocument.Parse(jsonString).RootElement;

                var startTime = DateTime.UtcNow;
                var boundParameters = await bindingService.BindParametersAsync(
                    methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                Assert.NotNull(boundParameters);
                Assert.Equal(expectedParameterCount, boundParameters.Length);
                Assert.True(duration.TotalMilliseconds < maxMilliseconds, 
                    $"Binding took {duration.TotalMilliseconds}ms, expected < {maxMilliseconds}ms");
            }
        }

        #endregion

        #region Schema Generation Helpers

        /// <summary>
        /// Helper methods for generating schema based on common web API patterns
        /// </summary>
        public static class SchemaGenerationHelpers
        {
            public static List<McpParameterInfo> CombineSchemas(params List<McpParameterInfo>[] schemas)
            {
                var combined = new List<McpParameterInfo>();
                foreach (var schema in schemas)
                {
                    if (schema != null)
                        combined.AddRange(schema);
                }
                return combined;
            }

            public static McpParameterInfo CreateStringParameter(
                string name,
                string source = "query",
                bool isRequired = false,
                string defaultValue = null)
            {
                return new McpParameterInfo
                {
                    Name = name,
                    Type = "string",
                    Source = source,
                    IsRequired = isRequired,
                    Default = defaultValue,
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", source == "route" ? "route_template_analysis" : "http_method_inference" }
                    }
                };
            }

            public static McpParameterInfo CreateIntegerParameter(
                string name,
                string source = "query",
                bool isRequired = false,
                int? defaultValue = null)
            {
                return new McpParameterInfo
                {
                    Name = name,
                    Type = "integer",
                    Source = source,
                    IsRequired = isRequired,
                    Default = defaultValue,
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", source == "route" ? "route_template_analysis" : "http_method_inference" }
                    }
                };
            }

            public static McpParameterInfo CreateBooleanParameter(
                string name,
                string source = "query",
                bool isRequired = false,
                bool? defaultValue = null)
            {
                return new McpParameterInfo
                {
                    Name = name,
                    Type = "boolean",
                    Source = source,
                    IsRequired = isRequired,
                    Default = defaultValue,
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", source == "route" ? "route_template_analysis" : "http_method_inference" }
                    }
                };
            }
        }

        #endregion
    }

    #region Example Usage Documentation

    /// <summary>
    /// Example usage of generic web API test patterns for common endpoint types
    /// </summary>
    public class GenericWebAPIPatternExamples
    {
        /// <summary>
        /// Example: Testing UserOrders endpoint with nested resource route pattern
        /// Pattern: /api/organizations/{orgId}/users/{userId}/orders
        /// </summary>
        public static void ExampleUserOrdersTest()
        {
            // This shows how to use the NestedResourceRoutePattern for any similar endpoint:
            // - UserProjects: /api/organizations/{orgId}/users/{userId}/projects
            // - UserPreferences: /api/organizations/{orgId}/users/{userId}/preferences
            // - OrderItems: /api/organizations/{orgId}/orders/{orderId}/items
            
            /*
            var actionDescriptor = NestedResourceRoutePattern.CreateActionDescriptor(
                "UserOrders",
                "GetOrders", 
                typeof(UserOrdersController),
                "orders",
                new List<ParameterDescriptor>
                {
                    new ControllerParameterDescriptor { Name = "pageSize", ParameterType = typeof(int) },
                    new ControllerParameterDescriptor { Name = "status", ParameterType = typeof(string) }
                });

            var schema = SchemaGenerationHelpers.CombineSchemas(
                new List<McpParameterInfo>
                {
                    SchemaGenerationHelpers.CreateIntegerParameter("orgId", "route", true),
                    SchemaGenerationHelpers.CreateIntegerParameter("userId", "route", true)
                },
                PaginationPattern.CreatePaginationSchema()
            );

            // Test successful binding
            var boundParameters = await NestedResourceRoutePattern.TestSuccessfulBinding(
                bindingService, methodInfo, schema, 
                orgId: 123, userId: 456,
                additionalParams: new Dictionary<string, object>
                {
                    { "pageSize", 20 },
                    { "status", "Active" }
                });

            NestedResourceRoutePattern.AssertRouteParametersCorrect(boundParameters);
            PaginationPattern.AssertPaginationDefaults(boundParameters, startIndex: 2);
            */
        }

        /// <summary>
        /// Example: Testing Product creation endpoint with complex request pattern
        /// Pattern: POST /api/products with complex DTO in body
        /// </summary>
        public static void ExampleComplexRequestTest()
        {
            /*
            var requestProperties = new Dictionary<string, McpParameterInfo>
            {
                { "name", SchemaGenerationHelpers.CreateStringParameter("name", "body", true) },
                { "category", SchemaGenerationHelpers.CreateStringParameter("category", "body", true) },
                { "price", SchemaGenerationHelpers.CreateDecimalParameter("price", "body", true) }
            };

            var schema = ComplexRequestPattern.CreateSchemaWithComplexRequest(
                "productDto", 
                typeof(CreateProductDto), 
                requestProperties,
                new List<string> { "name", "category", "price" });

            var requestObject = new
            {
                name = "Premium Widget",
                category = "Electronics",
                price = 99.99m
            };

            var boundDto = await ComplexRequestPattern.TestComplexObjectBinding<CreateProductDto>(
                bindingService, methodInfo, schema, requestObject);
                
            Assert.Equal("Premium Widget", boundDto.Name);
            */
        }
    }

    #endregion
}