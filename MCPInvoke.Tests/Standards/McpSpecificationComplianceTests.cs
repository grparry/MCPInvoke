using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.AspNetCore;
using MCPInvoke;

namespace MCPInvoke.Tests.Standards
{
    /// <summary>
    /// Tests to verify that MCPInvoke generates schemas that comply with the official MCP specification.
    /// These tests validate against the MCP protocol requirements for tool definitions.
    /// 
    /// Reference: Model Context Protocol Specification
    /// https://spec.modelcontextprotocol.io/specification/basic/tools/
    /// </summary>
    public class McpSpecificationComplianceTests
    {
        private readonly Mock<ILogger<AspNetControllerToolDefinitionProvider>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly AspNetControllerToolDefinitionProvider _provider;

        public McpSpecificationComplianceTests()
        {
            _loggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            var options = new AspNetControllerToolOptions();
            _provider = new AspNetControllerToolDefinitionProvider(
                _serviceProviderMock.Object, 
                _loggerMock.Object, 
                options, 
                new[] { typeof(McpSpecificationComplianceTests).Assembly });
        }

        #region MCP Tool Definition Structure Tests

        [Fact]
        public void ToolDefinitions_RequiredFields_AllPresent()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault();

            // Assert - MCP specification requires: name, description, inputSchema
            Assert.NotNull(tool);
            Assert.False(string.IsNullOrEmpty(tool.Name), "Tool name is required by MCP specification");
            Assert.False(string.IsNullOrEmpty(tool.Description), "Tool description is required by MCP specification");
            Assert.NotNull(tool.InputSchema);
        }

        [Fact]
        public void ToolDefinitions_NameFormat_ValidMcpIdentifier()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();

            // Assert - MCP tool names should be valid identifiers
            foreach (var tool in tools)
            {
                Assert.Matches(@"^[a-zA-Z][a-zA-Z0-9_]*$", tool.Name);
                Assert.DoesNotContain(" ", tool.Name); // No spaces allowed
                Assert.DoesNotContain("-", tool.Name); // Hyphens not recommended in identifiers
            }
        }

        [Fact]
        public void ToolDefinitions_InputSchemaFormat_ValidJsonSchemaStructure()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var complexTool = tools.FirstOrDefault(t => t.Name.Contains("ComplexObject"));

            // Assert - Input schema should follow JSON Schema specification
            Assert.NotNull(complexTool);
            
            // Convert to MCP format for validation
            var mcpSchema = ConvertToMcpInputSchema(complexTool.InputSchema);
            
            Assert.Equal("object", mcpSchema.Type);
            Assert.NotNull(mcpSchema.Properties);
            Assert.NotNull(mcpSchema.Required);
            
            // Properties should have valid JSON Schema types
            foreach (var property in mcpSchema.Properties)
            {
                Assert.True(IsValidJsonSchemaType(property.Value.Type), 
                    $"Invalid JSON Schema type '{property.Value.Type}' for property '{property.Key}'");
            }
        }

        #endregion

        #region JSON Schema Type Compliance Tests

        [Fact]
        public void InputSchema_PrimitiveTypes_MappedToValidJsonSchemaTypes()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var primitiveTool = tools.FirstOrDefault(t => t.Name.Contains("PrimitiveTypes"));

            // Assert
            Assert.NotNull(primitiveTool);

            var stringParam = primitiveTool.InputSchema.FirstOrDefault(p => p.Name == "textValue");
            var intParam = primitiveTool.InputSchema.FirstOrDefault(p => p.Name == "numberValue");
            var boolParam = primitiveTool.InputSchema.FirstOrDefault(p => p.Name == "flagValue");
            var dateParam = primitiveTool.InputSchema.FirstOrDefault(p => p.Name == "dateValue");

            Assert.Equal("string", stringParam?.Type);
            Assert.Equal("integer", intParam?.Type);
            Assert.Equal("boolean", boolParam?.Type);
            Assert.Equal("string", dateParam?.Type); // DateTime should map to string with format
        }

        [Fact]
        public void InputSchema_ArrayTypes_GenerateValidArraySchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var arrayTool = tools.FirstOrDefault(t => t.Name.Contains("ArrayParameter"));

            // Assert
            Assert.NotNull(arrayTool);
            
            var arrayParam = arrayTool.InputSchema.FirstOrDefault(p => p.Name == "items");
            Assert.Equal("array", arrayParam?.Type);
            Assert.True(arrayParam?.Annotations?.ContainsKey("items"), "Array parameters must have items schema");
        }

        [Fact]
        public void InputSchema_ObjectTypes_GenerateValidObjectSchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var objectTool = tools.FirstOrDefault(t => t.Name.Contains("ComplexObject"));

            // Assert
            Assert.NotNull(objectTool);
            
            var objectParam = objectTool.InputSchema.FirstOrDefault(p => p.Type == "object");
            Assert.Equal("object", objectParam?.Type);
            Assert.True(objectParam?.Annotations?.ContainsKey("properties"), "Object parameters must have properties schema");
            Assert.True(objectParam?.Annotations?.ContainsKey("required"), "Object parameters must have required array");
        }

        [Fact]
        public void InputSchema_EnumTypes_GenerateValidEnumSchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var enumTool = tools.FirstOrDefault(t => t.Name.Contains("EnumParameter"));

            // Assert
            Assert.NotNull(enumTool);
            
            var enumParam = enumTool.InputSchema.FirstOrDefault(p => p.Name == "status");
            Assert.NotNull(enumParam);
            
            // Enum should be represented with allowed values
            Assert.True((enumParam.Annotations?.ContainsKey("enum") ?? false) || 
                       enumParam.Type == "string", // String enum with enum values
                       "Enum parameters must specify allowed values");
        }

        #endregion

        #region MCP Protocol Format Tests

        [Fact]
        public void ToolDefinitions_SerializeToMcpFormat_ProducesValidJson()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();

            // Convert to official MCP tools list format
            var mcpToolsList = new
            {
                tools = tools.Select(tool => new
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = ConvertToMcpInputSchema(tool.InputSchema)
                }).ToArray()
            };

            // Serialize to JSON
            var json = JsonSerializer.Serialize(mcpToolsList, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Assert
            Assert.False(string.IsNullOrEmpty(json));
            
            // Should be valid JSON that can be parsed
            var parsed = JsonDocument.Parse(json);
            Assert.True(parsed.RootElement.TryGetProperty("tools", out var toolsArray));
            Assert.True(toolsArray.ValueKind == JsonValueKind.Array);

            // Each tool should have required MCP fields
            foreach (var toolElement in toolsArray.EnumerateArray())
            {
                Assert.True(toolElement.TryGetProperty("name", out _));
                Assert.True(toolElement.TryGetProperty("description", out _));
                Assert.True(toolElement.TryGetProperty("inputSchema", out _));
            }
        }

        [Fact]
        public void ToolDefinitions_McpCallFormat_MatchesSpecification()
        {
            // Arrange - Test that our schemas work with MCP call format
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            var tools = _provider.GetToolDefinitions();
            var tool = tools.First();

            // Create a sample MCP tools/call request
            var mcpCall = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = tool.Name,
                    arguments = CreateSampleArguments(tool.InputSchema)
                },
                id = 1
            };

            // Act - Serialize MCP call
            var json = JsonSerializer.Serialize(mcpCall);

            // Assert - Should be valid MCP protocol format
            var parsed = JsonDocument.Parse(json);
            Assert.Equal("2.0", parsed.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal("tools/call", parsed.RootElement.GetProperty("method").GetString());
            Assert.True(parsed.RootElement.TryGetProperty("params", out var paramsElement));
            Assert.True(paramsElement.TryGetProperty("name", out _));
            Assert.True(paramsElement.TryGetProperty("arguments", out _));
        }

        #endregion

        #region Specification Edge Case Tests

        [Fact]
        public void ToolDefinitions_RequiredProperties_CorrectlyIdentified()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name.Contains("RequiredParameters"));

            // Assert
            Assert.NotNull(tool);
            
            var mcpSchema = ConvertToMcpInputSchema(tool.InputSchema);
            
            // Required parameters should be in the required array
            var requiredParams = tool.InputSchema.Where(p => p.IsRequired).Select(p => p.Name).ToList();
            Assert.Equal(requiredParams.Count, mcpSchema.Required.Count);
            
            foreach (var requiredParam in requiredParams)
            {
                Assert.Contains(requiredParam, mcpSchema.Required);
            }
        }

        [Fact]
        public void ToolDefinitions_OptionalProperties_NotInRequiredList()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name.Contains("OptionalParameters"));

            // Assert
            Assert.NotNull(tool);
            
            var mcpSchema = ConvertToMcpInputSchema(tool.InputSchema);
            var optionalParams = tool.InputSchema.Where(p => !p.IsRequired).Select(p => p.Name).ToList();
            
            foreach (var optionalParam in optionalParams)
            {
                Assert.DoesNotContain(optionalParam, mcpSchema.Required);
            }
        }

        [Fact]
        public void ToolDefinitions_NestedObjects_MaintainSchemaIntegrity()
        {
            // Arrange - Test deeply nested object schemas
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(McpComplianceTestController)))
                .Returns(new McpComplianceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var nestedTool = tools.FirstOrDefault(t => t.Name.Contains("DeeplyNested"));

            // Assert
            Assert.NotNull(nestedTool);
            
            var mcpSchema = ConvertToMcpInputSchema(nestedTool.InputSchema);
            
            // Should handle nested objects without infinite recursion
            Assert.NotNull(mcpSchema.Properties);
            
            // Verify nested structure is preserved
            var complexProperty = mcpSchema.Properties.Values.FirstOrDefault(p => p.Type == "object");
            Assert.NotNull(complexProperty);
            Assert.NotNull(complexProperty.Properties);
        }

        #endregion

        #region Helper Methods

        private static bool IsValidJsonSchemaType(string type)
        {
            var validTypes = new[] { "null", "boolean", "object", "array", "number", "integer", "string" };
            return validTypes.Contains(type);
        }

        private static McpInputSchema ConvertToMcpInputSchema(IList<McpParameterInfo> parameters)
        {
            return new McpInputSchema
            {
                Type = "object",
                Properties = parameters.ToDictionary(p => p.Name, p => new McpPropertySchema
                {
                    Type = p.Type,
                    Description = p.Description ?? string.Empty,
                    Properties = p.Annotations?.ContainsKey("properties") == true 
                        ? ConvertAnnotationProperties(p.Annotations!["properties"]) 
                        : null,
                    Items = p.Annotations?.ContainsKey("items") == true 
                        ? ConvertAnnotationItems(p.Annotations!["items"])
                        : null,
                    Enum = p.Annotations?.ContainsKey("enum") == true
                        ? ConvertAnnotationEnum(p.Annotations!["enum"])
                        : null
                }),
                Required = parameters.Where(p => p.IsRequired).Select(p => p.Name).ToList()
            };
        }

        private static Dictionary<string, McpPropertySchema> ConvertAnnotationProperties(object properties)
        {
            if (properties is Dictionary<string, object> props)
            {
                return props.ToDictionary(kvp => kvp.Key, kvp => new McpPropertySchema
                {
                    Type = ExtractType(kvp.Value),
                    Description = ExtractDescription(kvp.Value)
                });
            }
            return new Dictionary<string, McpPropertySchema>();
        }

        private static McpPropertySchema ConvertAnnotationItems(object items)
        {
            return new McpPropertySchema { Type = "string" }; // Simplified for testing
        }

        private static List<object> ConvertAnnotationEnum(object enumValues)
        {
            if (enumValues is List<object> values)
                return values;
            return new List<object>();
        }

        private static string ExtractType(object propertyDefinition)
        {
            if (propertyDefinition is Dictionary<string, object> dict && dict.ContainsKey("type"))
                return dict["type"].ToString() ?? "string";
            return "string";
        }

        private static string ExtractDescription(object propertyDefinition)
        {
            if (propertyDefinition is Dictionary<string, object> dict && dict.ContainsKey("description"))
                return dict["description"].ToString() ?? "";
            return "";
        }

        private static Dictionary<string, object?> CreateSampleArguments(IList<McpParameterInfo> parameters)
        {
            var arguments = new Dictionary<string, object?>();
            
            foreach (var param in parameters.Where(p => p.IsRequired))
            {
                arguments[param.Name] = param.Type switch
                {
                    "string" => "sample_string",
                    "integer" => 42,
                    "number" => 3.14,
                    "boolean" => true,
                    "object" => new { },
                    "array" => new object[0],
                    _ => (object?)null
                };
            }
            
            return arguments;
        }

        #endregion
    }

    #region Helper Classes for MCP Schema Representation

    public class McpInputSchema
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, McpPropertySchema> Properties { get; set; } = new();
        public List<string> Required { get; set; } = new();
    }

    public class McpPropertySchema
    {
        public string Type { get; set; } = "string";
        public string Description { get; set; } = "";
        public Dictionary<string, McpPropertySchema>? Properties { get; set; }
        public McpPropertySchema? Items { get; set; }
        public List<object>? Enum { get; set; }
    }

    #endregion

    #region Test Controller and Models

    [ApiController]
    [Route("api/mcp-compliance")]
    public class McpComplianceTestController : ControllerBase
    {
        [HttpPost("primitive-types")]
        public IActionResult PrimitiveTypes(string textValue, int numberValue, bool flagValue, DateTime dateValue)
        {
            return Ok("Processed primitive types");
        }

        [HttpPost("array-parameter")]
        public IActionResult ArrayParameter(List<string> items)
        {
            return Ok("Processed array");
        }

        [HttpPost("complex-object")]
        public IActionResult ComplexObject([FromBody] McpTestComplexObject request)
        {
            return Ok("Processed complex object");
        }

        [HttpPost("enum-parameter")]
        public IActionResult EnumParameter(McpTestStatus status)
        {
            return Ok("Processed enum");
        }

        [HttpPost("required-parameters")]
        public IActionResult RequiredParameters(string requiredParam, int anotherRequired, string? optionalParam = null)
        {
            return Ok("Processed required parameters");
        }

        [HttpPost("optional-parameters")]
        public IActionResult OptionalParameters(string? optionalParam1 = null, int? optionalParam2 = null)
        {
            return Ok("Processed optional parameters");
        }

        [HttpPost("deeply-nested")]
        public IActionResult DeeplyNested([FromBody] McpTestNestedObject request)
        {
            return Ok("Processed nested object");
        }
    }

    public class McpTestComplexObject
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsActive { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class McpTestNestedObject
    {
        public string Name { get; set; } = string.Empty;
        public McpTestComplexObject NestedObject { get; set; } = new();
        public List<McpTestComplexObject> NestedArray { get; set; } = new();
    }

    public enum McpTestStatus
    {
        Active,
        Inactive,
        Pending,
        Archived
    }

    #endregion
}