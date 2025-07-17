using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.Services;
using MCPInvoke;
using MCPBuckle.Services;
using MCPBuckle.Models;

namespace MCPInvoke.Tests.Services
{
    public class McpBuckleToolDefinitionProviderTests
    {
        private readonly Mock<IControllerDiscoveryService> _toolScannerMock;
        private readonly Mock<ILogger<McpBuckleToolDefinitionProvider>> _loggerMock;
        private readonly Assembly _testAssembly;
        private readonly McpBuckleToolDefinitionProvider _provider;

        public McpBuckleToolDefinitionProviderTests()
        {
            _toolScannerMock = new Mock<IControllerDiscoveryService>();
            _loggerMock = new Mock<ILogger<McpBuckleToolDefinitionProvider>>();
            _testAssembly = Assembly.GetExecutingAssembly();
            _provider = new McpBuckleToolDefinitionProvider(
                _toolScannerMock.Object,
                _testAssembly,
                _loggerMock.Object);
        }

        [Fact]
        public void Constructor_NullToolScanner_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new McpBuckleToolDefinitionProvider(null!, _testAssembly, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_NullAssembly_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new McpBuckleToolDefinitionProvider(_toolScannerMock.Object, null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new McpBuckleToolDefinitionProvider(_toolScannerMock.Object, _testAssembly, null!));
        }

        [Fact]
        public void GetToolDefinitions_EmptyToolList_ReturnsEmptyList()
        {
            // Arrange
            _toolScannerMock
                .Setup(scanner => scanner.DiscoverTools())
                .Returns(new List<McpTool>());

            // Act
            var definitions = _provider.GetToolDefinitions();

            // Assert
            Assert.NotNull(definitions);
            Assert.Empty(definitions);
            _toolScannerMock.Verify(scanner => scanner.DiscoverTools(), Times.Once);
        }

        [Fact]
        public void GetToolDefinitions_ValidTools_ReturnsConvertedDefinitions()
        {
            // Arrange
            var mockTools = CreateMockTools();
            _toolScannerMock
                .Setup(scanner => scanner.DiscoverTools())
                .Returns(mockTools);

            // Act
            var definitions = _provider.GetToolDefinitions().ToList();

            // Assert
            Assert.Equal(2, definitions.Count);
            
            // Verify first tool
            var firstDef = definitions[0];
            Assert.Equal("TestTool1", firstDef.Name);
            Assert.Equal("Test tool 1 description", firstDef.Description);
            Assert.Equal("TestController", firstDef.HandlerTypeAssemblyQualifiedName);
            Assert.Equal("Method1", firstDef.MethodName);
            Assert.Equal(2, firstDef.InputSchema.Count);
            
            var param1 = firstDef.InputSchema.First(p => p.Name == "param1");
            Assert.Equal("string", param1.Type);
            Assert.Equal("First parameter", param1.Description);
            Assert.True(param1.IsRequired);
            
            var param2 = firstDef.InputSchema.First(p => p.Name == "param2");
            Assert.Equal("integer", param2.Type);
            Assert.Equal("Second parameter", param2.Description);
            Assert.False(param2.IsRequired);
        }

        [Fact]
        public void GetToolDefinitions_CachesResults_CallsDiscoverOnlyOnce()
        {
            // Arrange
            var mockTools = CreateMockTools();
            _toolScannerMock
                .Setup(scanner => scanner.DiscoverTools())
                .Returns(mockTools);

            // Act
            var definitions1 = _provider.GetToolDefinitions();
            var definitions2 = _provider.GetToolDefinitions();
            var definitions3 = _provider.GetToolDefinitions();

            // Assert
            Assert.Same(definitions1, definitions2);
            Assert.Same(definitions2, definitions3);
            _toolScannerMock.Verify(scanner => scanner.DiscoverTools(), Times.Once);
        }

        [Fact]
        public void GetToolDefinitions_HandlesToolsWithoutAnnotations_ExtractsFromMethodName()
        {
            // Arrange
            var toolsWithoutAnnotations = new List<McpTool>
            {
                new McpTool
                {
                    Name = "ToolWithoutAnnotations",
                    Description = "No annotations",
                    InputSchema = new McpSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, McpSchema>(),
                        Required = new List<string>()
                    },
                    Annotations = new Dictionary<string, object>() // Empty annotations
                }
            };
            
            _toolScannerMock
                .Setup(scanner => scanner.DiscoverTools())
                .Returns(toolsWithoutAnnotations);

            // Act
            var definitions = _provider.GetToolDefinitions().ToList();

            // Assert
            Assert.Single(definitions);
            var def = definitions[0];
            Assert.Equal("ToolWithoutAnnotations", def.Name);
            Assert.Equal("UNKNOWN_HANDLER_TYPE", def.HandlerTypeAssemblyQualifiedName);
            Assert.Equal("UNKNOWN_METHOD_NAME", def.MethodName);
        }

        [Fact]
        public void GetToolDefinitions_ToolDiscoveryThrows_ReturnsEmptyList()
        {
            // Arrange
            _toolScannerMock
                .Setup(scanner => scanner.DiscoverTools())
                .Throws(new Exception("Discovery failed"));

            // Act
            var definitions = _provider.GetToolDefinitions();

            // Assert
            Assert.NotNull(definitions);
            Assert.Empty(definitions);
            
            // Verify that error was logged - the actual logging message might be different
            _loggerMock.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error") || o.ToString()!.Contains("discovering") || o.ToString()!.Contains("tools")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void GetToolDefinitions_ComplexSchemaTypes_ConvertsCorrectly()
        {
            // Arrange
            var complexTool = new McpTool
            {
                Name = "ComplexTool",
                Description = "Tool with complex types",
                InputSchema = new McpSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpSchema>
                    {
                        ["arrayParam"] = new McpSchema
                        {
                            Type = "array",
                            Description = "Array parameter",
                            Items = new McpSchema { Type = "string" }
                        },
                        ["objectParam"] = new McpSchema
                        {
                            Type = "object",
                            Description = "Object parameter",
                            Properties = new Dictionary<string, McpSchema>
                            {
                                ["nested"] = new McpSchema { Type = "boolean" }
                            }
                        }
                    },
                    Required = new List<string> { "arrayParam" }
                },
                Annotations = new Dictionary<string, object>
                {
                    ["HandlerTypeAssemblyQualifiedName"] = "ComplexHandler",
                    ["MethodName"] = "ComplexMethod"
                }
            };
            
            _toolScannerMock
                .Setup(scanner => scanner.DiscoverTools())
                .Returns(new List<McpTool> { complexTool });

            // Act
            var definitions = _provider.GetToolDefinitions().ToList();

            // Assert
            var def = definitions[0];
            var arrayParam = def.InputSchema.First(p => p.Name == "arrayParam");
            Assert.Equal("array", arrayParam.Type);
            Assert.True(arrayParam.IsRequired);
            
            var objectParam = def.InputSchema.First(p => p.Name == "objectParam");
            Assert.Equal("object", objectParam.Type);
            Assert.False(objectParam.IsRequired);
        }

        private static List<McpTool> CreateMockTools()
        {
            return new List<McpTool>
            {
                new McpTool
                {
                    Name = "TestTool1",
                    Description = "Test tool 1 description",
                    InputSchema = new McpSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, McpSchema>
                        {
                            ["param1"] = new McpSchema
                            {
                                Type = "string",
                                Description = "First parameter"
                            },
                            ["param2"] = new McpSchema
                            {
                                Type = "integer",
                                Description = "Second parameter"
                            }
                        },
                        Required = new List<string> { "param1" }
                    },
                    Annotations = new Dictionary<string, object>
                    {
                        ["HandlerTypeAssemblyQualifiedName"] = "TestController",
                        ["MethodName"] = "Method1"
                    }
                },
                new McpTool
                {
                    Name = "TestTool2",
                    Description = "Test tool 2 description",
                    InputSchema = new McpSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, McpSchema>
                        {
                            ["data"] = new McpSchema
                            {
                                Type = "object",
                                Description = "Data object"
                            }
                        },
                        Required = new List<string>()
                    },
                    Annotations = new Dictionary<string, object>
                    {
                        ["HandlerTypeAssemblyQualifiedName"] = "TestController",
                        ["MethodName"] = "Method2"
                    }
                }
            };
        }
    }
}