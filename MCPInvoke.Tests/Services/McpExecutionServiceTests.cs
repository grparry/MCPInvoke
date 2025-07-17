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
    }
}