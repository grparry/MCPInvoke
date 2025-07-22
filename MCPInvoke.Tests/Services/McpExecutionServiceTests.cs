using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.Services;
using MCPInvoke.Models;
using MCPInvoke;

namespace MCPInvoke.Tests.Services
{
    public class McpExecutionServiceTests
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly Mock<IMcpToolDefinitionProvider> _toolDefinitionProviderMock;
        private readonly Mock<ILogger<McpExecutionService>> _loggerMock;
        private readonly McpExecutionService _executionService;

        public McpExecutionServiceTests()
        {
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            _toolDefinitionProviderMock = new Mock<IMcpToolDefinitionProvider>();
            _loggerMock = new Mock<ILogger<McpExecutionService>>();
            
            // Setup empty tool definitions by default
            _toolDefinitionProviderMock
                .Setup(provider => provider.GetToolDefinitions())
                .Returns(new List<McpToolDefinition>());
            
            _executionService = new McpExecutionService(
                _serviceScopeFactoryMock.Object,
                _toolDefinitionProviderMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public void Constructor_NullServiceScopeFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new McpExecutionService(null!, _toolDefinitionProviderMock.Object, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_NullToolDefinitionProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new McpExecutionService(_serviceScopeFactoryMock.Object, null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new McpExecutionService(_serviceScopeFactoryMock.Object, _toolDefinitionProviderMock.Object, null!));
        }

        [Fact]
        public void RegisterTool_NullTool_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _executionService.RegisterTool(null!));
        }

        [Fact]
        public void RegisterTool_EmptyToolName_ThrowsArgumentException()
        {
            // Arrange
            var tool = new RegisteredTool(
                "",
                typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.TestMethod))!,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _executionService.RegisterTool(tool));
        }

        [Fact]
        public void RegisterTool_ValidTool_RegistersSuccessfully()
        {
            // Arrange
            var tool = new RegisteredTool(
                "TestTool",
                typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.TestMethod))!,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>());

            // Act
            _executionService.RegisterTool(tool);

            // Assert
            _loggerMock.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully registered tool: TestTool")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessRequestAsync_ToolsList_ReturnsAvailableTools()
        {
            // Arrange
            var toolDefinitions = new List<McpToolDefinition>
            {
                new McpToolDefinition
                {
                    Name = "Tool1",
                    Description = "First tool",
                    InputSchema = new List<McpParameterInfo>
                    {
                        new McpParameterInfo { Name = "param1", Type = "string", IsRequired = true }
                    }
                },
                new McpToolDefinition
                {
                    Name = "Tool2",
                    Description = "Second tool",
                    InputSchema = new List<McpParameterInfo>()
                }
            };
            
            _toolDefinitionProviderMock
                .Setup(provider => provider.GetToolDefinitions())
                .Returns(toolDefinitions);
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/list",
                id = 1
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var tools));
            
            var toolsArray = tools.EnumerateArray().ToList();
            Assert.Equal(2, toolsArray.Count);
            
            Assert.Equal("Tool1", toolsArray[0].GetProperty("name").GetString());
            Assert.Equal("First tool", toolsArray[0].GetProperty("description").GetString());
        }

        [Fact]
        public async Task ProcessRequestAsync_ToolsCall_ExecutesTool()
        {
            // Arrange
            var testHandler = new TestToolHandler();
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.TestMethod))!;
            
            var tool = new RegisteredTool(
                "TestTool",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    ["message"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(string), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TestToolHandler)))
                .Returns(testHandler);
            serviceScopeMock
                .Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock
                .Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "TestTool",
                    arguments = new { message = "Hello" }
                },
                id = 2
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            // MCPInvoke returns the raw method result, not MCP-formatted content
            Assert.Equal("Processed: Hello", result.GetString());
        }

        [Fact]
        public async Task ProcessRequestAsync_InvalidJson_ReturnsParseError()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act
            var response = await _executionService.ProcessRequestAsync(invalidJson);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32700, error.GetProperty("code").GetInt32()); // Parse error
        }

        [Fact]
        public async Task ProcessRequestAsync_UnknownMethod_ReturnsMethodNotFound()
        {
            // Arrange
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "unknown/method",
                id = 3
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32601, error.GetProperty("code").GetInt32()); // Method not found
        }

        [Fact]
        public async Task ProcessRequestAsync_ToolNotFound_ReturnsError()
        {
            // Arrange
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "NonExistentTool",
                    arguments = new { }
                },
                id = 4
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32601, error.GetProperty("code").GetInt32()); // Method not found
            Assert.Contains("not found", error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProcessRequestAsync_MissingRequiredParameter_ReturnsError()
        {
            // Arrange
            var tool = new RegisteredTool(
                "RequiredParamTool",
                typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.TestMethod))!,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    ["requiredParam"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(string), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "RequiredParamTool",
                    arguments = new { } // Missing required parameter
                },
                id = 5
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32603, error.GetProperty("code").GetInt32()); // Internal error
        }

        [Fact]
        public void Constructor_LoadsToolsFromProvider()
        {
            // Arrange
            var toolDefinitions = new List<McpToolDefinition>
            {
                new McpToolDefinition
                {
                    Name = "LoadedTool",
                    Description = "Tool loaded from provider",
                    HandlerTypeAssemblyQualifiedName = typeof(TestToolHandler).AssemblyQualifiedName!,
                    MethodName = nameof(TestToolHandler.TestMethod),
                    InputSchema = new List<McpParameterInfo>()
                }
            };
            
            var providerMock = new Mock<IMcpToolDefinitionProvider>();
            providerMock
                .Setup(provider => provider.GetToolDefinitions())
                .Returns(toolDefinitions);

            // Act
            var service = new McpExecutionService(
                _serviceScopeFactoryMock.Object,
                providerMock.Object,
                _loggerMock.Object);

            // Assert
            _loggerMock.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully registered tool from provider: LoadedTool")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessRequestAsync_StaticMethod_ExecutesSuccessfully()
        {
            // Arrange
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.StaticTestMethod))!;
            
            var tool = new RegisteredTool(
                "StaticTool",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    ["value"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(int), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "StaticTool",
                    arguments = new { value = 42 }
                },
                id = 6
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            // MCPInvoke returns the raw method result
            Assert.Equal("Result: 84", result.GetString());
        }

        [Fact]
        public async Task ProcessRequestAsync_ComplexObject_DeserializesCorrectly()
        {
            // Arrange - This test specifically addresses the JsonElement conversion bug
            var testHandler = new TestToolHandler();
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.ProcessComplexObject))!;
            
            var tool = new RegisteredTool(
                "ComplexObjectTool",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    // Note: We're using generic "object" type mapping to simulate the original bug scenario
                    ["request"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(object), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TestToolHandler)))
                .Returns(testHandler);
            serviceScopeMock
                .Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock
                .Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            var complexRequest = new
            {
                name = "TestObject",
                count = 42,
                isActive = true,
                requestType = 1 // TypeB enum value
            };
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "ComplexObjectTool",
                    arguments = new { request = complexRequest }
                },
                id = 7
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            var resultString = result.GetString();
            Assert.Contains("Complex: TestObject", resultString);
            Assert.Contains("Count: 42", resultString);
            Assert.Contains("Active: True", resultString);
            Assert.Contains("Type: TypeB", resultString);
        }

        [Fact]
        public async Task ProcessRequestAsync_WorkflowExecutionRequest_ReplicatesBugScenario()
        {
            // Arrange - This test replicates the exact Workflow3ExecutionRequest scenario from the bug report
            var testHandler = new TestToolHandler();
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.ExecuteWorkflow))!;
            
            var tool = new RegisteredTool(
                "ExecuteWorkflow",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    // Simulate the bug: generic object mapping instead of the specific type
                    ["request"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(object), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TestToolHandler)))
                .Returns(testHandler);
            serviceScopeMock
                .Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock
                .Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            // Create a realistic workflow request that would have caused the original bug
            var workflowRequest = new
            {
                tenantId = 1,
                customerId = 12345,
                workflowName = "customer-insights",
                version = "1.0",
                shadowMode = false,
                parameters = new Dictionary<string, object>
                {
                    ["includeTransactionHistory"] = true,
                    ["analysisDepth"] = "full"
                }
            };
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "ExecuteWorkflow",
                    arguments = new { request = workflowRequest }
                },
                id = 8
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            var resultString = result.GetString();
            Assert.Contains("Workflow: customer-insights", resultString);
            Assert.Contains("Tenant 1", resultString);
            Assert.Contains("Customer 12345", resultString);
            Assert.Contains("Shadow: False", resultString);
            Assert.Contains("Params: 2", resultString);
        }

        [Fact]
        public async Task ProcessRequestAsync_NestedComplexObject_DeserializesCorrectly()
        {
            // Arrange - Test nested objects with complex hierarchies
            var testHandler = new TestToolHandler();
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.ProcessNestedComplexObject))!;
            
            var tool = new RegisteredTool(
                "NestedObjectTool",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    ["request"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(object), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TestToolHandler)))
                .Returns(testHandler);
            serviceScopeMock
                .Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock
                .Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            var nestedRequest = new
            {
                mainData = new
                {
                    name = "MainObject",
                    count = 25,
                    isActive = true,
                    requestType = 2 // TypeC
                },
                subData = new
                {
                    count = 10,
                    description = "Sub object data"
                },
                items = new[] { "item1", "item2", "item3" }
            };
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "NestedObjectTool",
                    arguments = new { request = nestedRequest }
                },
                id = 9
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            var resultString = result.GetString();
            Assert.Contains("Nested: MainObject", resultString);
            Assert.Contains("SubCount: 10", resultString);
            Assert.Contains("Items: 3", resultString);
        }

        [Fact]
        public async Task ProcessRequestAsync_EnumParameter_DeserializesCorrectly()
        {
            // Arrange - Test enum deserialization which was part of the complex object issue
            var testHandler = new TestToolHandler();
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.ProcessEnumParameter))!;
            
            var tool = new RegisteredTool(
                "EnumTool",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    ["enumValue"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(int), true, null) // Simulate incorrect type mapping
                });
            
            _executionService.RegisterTool(tool);
            
            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TestToolHandler)))
                .Returns(testHandler);
            serviceScopeMock
                .Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock
                .Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "EnumTool",
                    arguments = new { enumValue = 1 } // TypeB as numeric value
                },
                id = 10
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            var resultString = result.GetString();
            Assert.Equal("Enum: TypeB", resultString);
        }

        [Fact]
        public async Task ProcessRequestAsync_ComplexObjectTypeMapMismatch_UsesPararameterTypeNotMappedType()
        {
            // Arrange - This is the core test that proves our fix works
            // The test deliberately uses wrong type mapping to ensure the fix kicks in
            var testHandler = new TestToolHandler();
            var methodInfo = typeof(TestToolHandler).GetMethod(nameof(TestToolHandler.ProcessComplexObject))!;
            
            var tool = new RegisteredTool(
                "TypeMapMismatchTool",
                methodInfo,
                typeof(TestToolHandler),
                new Dictionary<string, RegisteredTool.McpToolSchemaPropertyPlaceholder>
                {
                    // CRITICAL: We're deliberately mapping to string instead of ComplexTestRequest
                    // This simulates the bug where type mapping was incorrect
                    ["request"] = new RegisteredTool.McpToolSchemaPropertyPlaceholder(typeof(string), true, null)
                });
            
            _executionService.RegisterTool(tool);
            
            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TestToolHandler)))
                .Returns(testHandler);
            serviceScopeMock
                .Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock
                .Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            var complexRequest = new
            {
                name = "FixedObject",
                count = 100,
                isActive = false,
                requestType = 0 // TypeA
            };
            
            var jsonRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "TypeMapMismatchTool",
                    arguments = new { request = complexRequest }
                },
                id = 11
            });

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            // If our fix works, this should succeed even though the mapping type is wrong
            var resultString = result.GetString();
            Assert.Contains("Complex: FixedObject", resultString);
            Assert.Contains("Count: 100", resultString);
            Assert.Contains("Active: False", resultString);
            Assert.Contains("Type: TypeA", resultString);
        }
    }

    // Test helper classes
    public class TestToolHandler
    {
        public string TestMethod(string message)
        {
            return $"Processed: {message}";
        }

        public static string StaticTestMethod(int value)
        {
            return $"Result: {value * 2}";
        }

        // Complex object method for testing JsonElement conversion
        public string ProcessComplexObject(ComplexTestRequest request)
        {
            return $"Complex: {request.Name}, Count: {request.Count}, Active: {request.IsActive}, Type: {request.RequestType}";
        }

        // Method with nested complex objects
        public string ProcessNestedComplexObject(NestedTestRequest request)
        {
            return $"Nested: {request.MainData.Name}, SubCount: {request.SubData.Count}, Items: {request.Items?.Count ?? 0}";
        }

        // Method with enum parameter
        public string ProcessEnumParameter(TestEnum enumValue)
        {
            return $"Enum: {enumValue}";
        }

        // Method that mimics Workflow3ExecutionRequest
        public string ExecuteWorkflow(WorkflowExecutionRequest request)
        {
            return $"Workflow: {request.WorkflowName} for Tenant {request.TenantId}, Customer {request.CustomerId}, Shadow: {request.ShadowMode}, Params: {request.Parameters?.Count ?? 0}";
        }
    }

    // Test models that replicate real-world complex objects
    public class ComplexTestRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsActive { get; set; }
        public TestEnum RequestType { get; set; }
    }

    public class NestedTestRequest
    {
        public ComplexTestRequest MainData { get; set; } = new();
        public SubTestData SubData { get; set; } = new();
        public List<string> Items { get; set; } = new();
    }

    public class SubTestData
    {
        public int Count { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public enum TestEnum
    {
        TypeA = 0,
        TypeB = 1,
        TypeC = 2
    }

    // Replica of Workflow3ExecutionRequest to test the exact scenario from the bug report
    public class WorkflowExecutionRequest
    {
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string WorkflowName { get; set; } = string.Empty;
        public string Version { get; set; } = "";
        public bool ShadowMode { get; set; } = false;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}