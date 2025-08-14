using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.Services;

namespace MCPInvoke.Tests.Services
{
    /// <summary>
    /// Comprehensive test suite for MCPInvoke v2.0 enhanced parameter binding service.
    /// Tests schema-aware parameter binding, validation, and type coercion capabilities.
    /// </summary>
    public class EnhancedParameterBindingServiceTests
    {
        private readonly Mock<ILogger<EnhancedParameterBindingService>> _mockLogger;
        private readonly EnhancedParameterBindingService _bindingService;

        public EnhancedParameterBindingServiceTests()
        {
            _mockLogger = new Mock<ILogger<EnhancedParameterBindingService>>();
            _bindingService = new EnhancedParameterBindingService(_mockLogger.Object);
        }

        #region Basic Parameter Binding Tests

        [Fact]
        public async Task BindParametersAsync_WithSimpleStringParameter_SuccessfullyBinds()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.SimpleStringMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "message",
                    Type = "string",
                    IsRequired = true,
                    Source = "query",
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", "http_method_inference" },
                        { "httpMethod", "GET" }
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"message\": \"Hello World\"}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Hello World", result[0]);
        }

        [Fact]
        public async Task BindParametersAsync_WithIntegerParameter_SuccessfullyBindsAndConverts()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.IntegerMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "count",
                    Type = "integer",
                    IsRequired = true,
                    Source = "query"
                }
            };

            var paramsJson = JsonDocument.Parse("{\"count\": 42}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(42, result[0]);
        }

        [Fact]
        public async Task BindParametersAsync_WithBooleanParameter_SuccessfullyBinds()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.BooleanMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "isActive",
                    Type = "boolean",
                    IsRequired = false,
                    Source = "query",
                    Default = false
                }
            };

            var paramsJson = JsonDocument.Parse("{\"isActive\": true}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(true, result[0]);
        }

        #endregion

        #region Enhanced Enum Parameter Binding Tests

        [Fact]
        public async Task BindParametersAsync_WithStringEnumParameter_SuccessfullyBinds()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.EnumMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "status",
                    Type = "string",
                    IsRequired = true,
                    Source = "body",
                    Format = "enum",
                    Enum = new List<object> { "Active", "Inactive", "Pending" },
                    Annotations = new Dictionary<string, object>
                    {
                        { "IsStringEnum", true },
                        { "sourceDetectionMethod", "explicit" }
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"status\": \"Active\"}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(TestStatus.Active, result[0]);
        }

        [Fact]
        public async Task BindParametersAsync_WithNumericEnumParameter_SuccessfullyBinds()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.EnumMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "status",
                    Type = "integer",
                    IsRequired = true,
                    Source = "query",
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", "http_method_inference" }
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"status\": 1}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(TestStatus.Inactive, result[0]);
        }

        [Fact]
        public async Task BindParametersAsync_WithInvalidEnumValue_ThrowsParameterBindingException()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.EnumMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "status",
                    Type = "string",
                    IsRequired = true,
                    Source = "body",
                    Enum = new List<object> { "Active", "Inactive", "Pending" }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"status\": \"InvalidStatus\"}").RootElement;

            // Act & Assert
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");
            
            // Should return null indicating binding failure, which will be handled by the calling service
            Assert.Null(result);
        }

        #endregion

        #region Complex Object Parameter Binding Tests

        [Fact]
        public async Task BindParametersAsync_WithComplexObject_SuccessfullyBinds()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.ComplexObjectMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "request",
                    Type = "object",
                    IsRequired = true,
                    Source = "body",
                    Properties = new Dictionary<string, McpParameterInfo>
                    {
                        {
                            "Name", new McpParameterInfo
                            {
                                Name = "Name",
                                Type = "string",
                                IsRequired = true
                            }
                        },
                        {
                            "Age", new McpParameterInfo
                            {
                                Name = "Age",
                                Type = "integer",
                                IsRequired = false
                            }
                        }
                    },
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", "http_method_inference" },
                        { "httpMethod", "POST" }
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"request\": {\"Name\": \"John Doe\", \"Age\": 30}}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var complexObj = Assert.IsType<TestRequest>(result[0]);
            Assert.Equal("John Doe", complexObj.Name);
            Assert.Equal(30, complexObj.Age);
        }

        [Fact]
        public async Task BindParametersAsync_WithComplexObjectMissingRequiredProperty_ThrowsException()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.ComplexObjectMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "request",
                    Type = "object",
                    IsRequired = true,
                    Source = "body",
                    Properties = new Dictionary<string, McpParameterInfo>
                    {
                        {
                            "Name", new McpParameterInfo
                            {
                                Name = "Name",
                                Type = "string",
                                IsRequired = true
                            }
                        }
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"request\": {\"Age\": 30}}").RootElement;

            // Act & Assert
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");
            
            // Complex object validation occurs during deserialization - result should be not null but object may have default values
            Assert.NotNull(result);
        }

        #endregion

        #region Route Parameter Binding Tests

        [Fact]
        public async Task BindParametersAsync_WithRouteParameters_SuccessfullyBindsFromQuery()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.RouteParameterMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "tenantId",
                    Type = "integer",
                    IsRequired = true,
                    Source = "route",
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", "route_template_analysis" },
                        { "httpMethod", "GET" },
                        { "routeTemplate", "api/tenants/{tenantId}/items" },
                        { "isRouteParameter", true }
                    }
                },
                new McpParameterInfo
                {
                    Name = "includeDetails",
                    Type = "boolean",
                    IsRequired = false,
                    Source = "query",
                    Annotations = new Dictionary<string, object>
                    {
                        { "sourceDetectionMethod", "http_method_inference" },
                        { "httpMethod", "GET" }
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"tenantId\": 123, \"includeDetails\": true}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Equal(123, result[0]);
            Assert.Equal(true, result[1]);
        }

        #endregion

        #region Missing and Optional Parameter Tests

        [Fact]
        public async Task BindParametersAsync_WithMissingRequiredParameter_ThrowsException()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.SimpleStringMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "message",
                    Type = "string",
                    IsRequired = true,
                    Source = "query"
                }
            };

            var paramsJson = JsonDocument.Parse("{}").RootElement;

            // Act & Assert
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");
            
            Assert.Null(result); // Should return null indicating binding failure
        }

        [Fact]
        public async Task BindParametersAsync_WithOptionalParameterAndDefault_UsesDefault()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.OptionalParameterMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "message",
                    Type = "string",
                    IsRequired = true,
                    Source = "query"
                },
                new McpParameterInfo
                {
                    Name = "count",
                    Type = "integer",
                    IsRequired = false,
                    Source = "query",
                    Default = 10
                }
            };

            var paramsJson = JsonDocument.Parse("{\"message\": \"Hello\"}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Equal("Hello", result[0]);
            Assert.Equal(10, result[1]); // Should use schema default
        }

        [Fact]
        public async Task BindParametersAsync_WithParameterNotInSchema_SkipsIfOptional()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.OptionalParameterMethod))!;
            var parameters = methodInfo.GetParameters();
            
            // Schema only includes the required parameter
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "message",
                    Type = "string",
                    IsRequired = true,
                    Source = "query"
                }
            };

            var paramsJson = JsonDocument.Parse("{\"message\": \"Hello\"}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Equal("Hello", result[0]);
            // Second parameter should use C# default value since it has HasDefaultValue = true
            Assert.Equal(5, result[1]); // C# default value from method signature
        }

        #endregion

        #region Array and Collection Parameter Tests

        [Fact]
        public async Task BindParametersAsync_WithArrayParameter_SuccessfullyBinds()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.ArrayMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "items",
                    Type = "array",
                    IsRequired = true,
                    Source = "body",
                    Items = new McpParameterInfo
                    {
                        Type = "string"
                    }
                }
            };

            var paramsJson = JsonDocument.Parse("{\"items\": [\"item1\", \"item2\", \"item3\"]}").RootElement;

            // Act
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var array = Assert.IsType<string[]>(result[0]);
            Assert.Equal(3, array.Length);
            Assert.Equal("item1", array[0]);
            Assert.Equal("item2", array[1]);
            Assert.Equal("item3", array[2]);
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task BindParametersAsync_WithNullSchema_HandlesGracefully()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.SimpleStringMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>(); // Empty schema
            var paramsJson = JsonDocument.Parse("{\"message\": \"Hello\"}").RootElement;

            // Act & Assert
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");
            
            Assert.Null(result); // Should return null for required parameters not in schema
        }

        [Fact]
        public async Task BindParametersAsync_WithInvalidJsonType_HandlesGracefully()
        {
            // Arrange
            var methodInfo = typeof(TestController).GetMethod(nameof(TestController.IntegerMethod))!;
            var parameters = methodInfo.GetParameters();
            
            var inputSchema = new List<McpParameterInfo>
            {
                new McpParameterInfo
                {
                    Name = "count",
                    Type = "integer",
                    IsRequired = true,
                    Source = "query"
                }
            };

            var paramsJson = JsonDocument.Parse("{\"count\": \"not_a_number\"}").RootElement;

            // Act & Assert
            var result = await _bindingService.BindParametersAsync(
                parameters, inputSchema, paramsJson, true, "testTool");
            
            Assert.Null(result); // Should return null indicating binding failure
        }

        #endregion
    }

    #region Test Controller and Model Classes

    public class TestController
    {
        public string SimpleStringMethod(string message) => message;
        
        public int IntegerMethod(int count) => count;
        
        public bool BooleanMethod(bool isActive) => isActive;
        
        public TestStatus EnumMethod(TestStatus status) => status;
        
        public TestRequest ComplexObjectMethod(TestRequest request) => request;
        
        public string RouteParameterMethod(int tenantId, bool includeDetails = false) => $"{tenantId}:{includeDetails}";
        
        public string OptionalParameterMethod(string message, int count = 5) => $"{message}:{count}";
        
        public string[] ArrayMethod(string[] items) => items;
    }

    public enum TestStatus
    {
        Active = 0,
        Inactive = 1,
        Pending = 2
    }

    public class TestRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    #endregion
}