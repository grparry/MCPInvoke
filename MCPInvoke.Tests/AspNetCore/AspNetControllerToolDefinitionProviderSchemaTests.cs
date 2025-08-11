using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.AspNetCore;
using MCPInvoke;

namespace MCPInvoke.Tests.AspNetCore
{
    /// <summary>
    /// Comprehensive test suite for AspNetControllerToolDefinitionProvider schema generation fixes.
    /// Tests the three critical areas: route parameter extraction, complex object schema generation, 
    /// and parameter source detection.
    /// </summary>
    public class AspNetControllerToolDefinitionProviderSchemaTests
    {
        private readonly Mock<ILogger<AspNetControllerToolDefinitionProvider>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly AspNetControllerToolDefinitionProvider _provider;

        public AspNetControllerToolDefinitionProviderSchemaTests()
        {
            _loggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            
            var options = new AspNetControllerToolOptions();
            // Pass the test assembly so it can find test controllers
            _provider = new AspNetControllerToolDefinitionProvider(
                _serviceProviderMock.Object, 
                _loggerMock.Object, 
                options, 
                new[] { typeof(AspNetControllerToolDefinitionProviderSchemaTests).Assembly });
        }

        #region Route Parameter Extraction Tests

        [Fact]
        public void GetToolDefinitions_SimpleRouteParameter_ExtractsCorrectly()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(SimpleRouteController)))
                .Returns(new SimpleRouteController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "SimpleRoute_GetById");

            // Assert
            Assert.NotNull(tool);
            Assert.Contains(tool.InputSchema, p => p.Name == "id" && p.Type == "integer");
            
            // Verify parameter source annotation
            var idParam = tool.InputSchema.First(p => p.Name == "id");
            Assert.True(idParam.Annotations?.ContainsKey("source"));
            Assert.Equal("route", idParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_MultipleRouteParameters_ExtractsAll()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(SimpleRouteController)))
                .Returns(new SimpleRouteController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "SimpleRoute_GetByTenantAndCustomer");

            // Assert
            Assert.NotNull(tool);
            Assert.Contains(tool.InputSchema, p => p.Name == "tenantId" && p.Type == "integer");
            Assert.Contains(tool.InputSchema, p => p.Name == "customerId" && p.Type == "integer");
            
            // Both should be route parameters
            var tenantParam = tool.InputSchema.First(p => p.Name == "tenantId");
            var customerParam = tool.InputSchema.First(p => p.Name == "customerId");
            Assert.Equal("route", tenantParam.Annotations!["source"]);
            Assert.Equal("route", customerParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_ComplexRouteTemplate_ParsesCorrectly()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexRouteController)))
                .Returns(new ComplexRouteController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexRoute_UpdateStepDefinition");

            // Assert
            Assert.NotNull(tool);
            
            // Should extract stepDefinitionId from route template "step-definition/{stepDefinitionId}"
            var routeParam = tool.InputSchema.FirstOrDefault(p => p.Name == "stepDefinitionId");
            Assert.NotNull(routeParam);
            Assert.Equal("integer", routeParam.Type);
            Assert.Equal("route", routeParam.Annotations!["source"]);
            
            // Should also have the body parameter
            var bodyParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(bodyParam);
            Assert.Equal("object", bodyParam.Type);
            Assert.Equal("body", bodyParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_RouteParameterWithConstraints_ExtractsWithoutConstraints()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexRouteController)))
                .Returns(new ComplexRouteController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexRoute_GetWithConstraints");

            // Assert
            Assert.NotNull(tool);
            
            // Should extract id from route template "items/{id:int:min(1)}"
            var idParam = tool.InputSchema.FirstOrDefault(p => p.Name == "id");
            Assert.NotNull(idParam);
            Assert.Equal("integer", idParam.Type);
            Assert.Equal("route", idParam.Annotations!["source"]);
        }

        #endregion

        #region Complex Object Schema Generation Tests

        [Fact]
        public void GetToolDefinitions_ComplexObjectParameter_GeneratesDetailedSchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexObjectController)))
                .Returns(new ComplexObjectController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexObject_ProcessRequest");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(requestParam);
            Assert.Equal("object", requestParam.Type);
            
            // Should have detailed properties schema
            Assert.True(requestParam.Annotations?.ContainsKey("properties"));
            var properties = requestParam!.Annotations!["properties"] as Dictionary<string, object>;
            Assert.NotNull(properties);
            
            // Verify specific properties are mapped
            Assert.True(properties.ContainsKey("StepCode"));
            Assert.True(properties.ContainsKey("StepName"));
            Assert.True(properties.ContainsKey("Description"));
            Assert.True(properties!.ContainsKey("Definition"));
            
            // Verify required properties list
            Assert.True(requestParam.Annotations?.ContainsKey("required"));
            var required = requestParam.Annotations!["required"] as List<string>;
            Assert.NotNull(required);
        }

        [Fact]
        public void GetToolDefinitions_NestedComplexObject_GeneratesRecursiveSchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexObjectController)))
                .Returns(new ComplexObjectController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexObject_ProcessRequest");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            var properties = requestParam!.Annotations!["properties"] as Dictionary<string, object>;
            
            // The "Definition" property should be a nested complex object
            Assert.True(properties!.ContainsKey("Definition"));
            
            var definitionProperty = properties!["Definition"] as Dictionary<string, object>;
            Assert.NotNull(definitionProperty);
            Assert.Equal("object", definitionProperty!["type"]);
            
            // Should have nested properties for ComposableStepDefinition
            Assert.True(definitionProperty.ContainsKey("properties"));
            var nestedProperties = definitionProperty!["properties"] as Dictionary<string, object>;
            Assert.NotNull(nestedProperties);
            Assert.True(nestedProperties.ContainsKey("StepCode"));
            Assert.True(nestedProperties.ContainsKey("Processor"));
        }

        [Fact]
        public void GetToolDefinitions_EnumProperty_GeneratesEnumSchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexObjectController)))
                .Returns(new ComplexObjectController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexObject_ProcessEnumRequest");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            var properties = requestParam!.Annotations!["properties"] as Dictionary<string, object>;
            
            // The "Status" property should be an enum
            Assert.True(properties!.ContainsKey("Status"));
            var statusProperty = properties!["Status"] as Dictionary<string, object>;
            Assert.NotNull(statusProperty);
            
            // Should specify enum values
            Assert.True(statusProperty.ContainsKey("enum"));
            var enumValues = statusProperty!["enum"] as List<object>;
            Assert.NotNull(enumValues);
            Assert.Contains("Active", enumValues);
            Assert.Contains("Inactive", enumValues);
            Assert.Contains("Pending", enumValues);
        }

        [Fact]
        public void GetToolDefinitions_ArrayProperty_GeneratesArraySchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexObjectController)))
                .Returns(new ComplexObjectController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexObject_ProcessBatchRequest");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            var properties = requestParam!.Annotations!["properties"] as Dictionary<string, object>;
            
            // The "Items" property should be an array
            Assert.True(properties!.ContainsKey("Items"));
            var itemsProperty = properties!["Items"] as Dictionary<string, object>;
            Assert.NotNull(itemsProperty);
            Assert.Equal("array", itemsProperty!["type"]);
            
            // Should have items schema for array elements
            Assert.True(itemsProperty.ContainsKey("items"));
        }

        #endregion

        #region Parameter Source Detection Tests

        [Fact]
        public void GetToolDefinitions_FromBodyAttribute_DetectsBodySource()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ParameterSourceController)))
                .Returns(new ParameterSourceController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ParameterSource_ExplicitBodyParam");

            // Assert
            Assert.NotNull(tool);
            
            var bodyParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(bodyParam);
            Assert.Equal("body", bodyParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_FromQueryAttribute_DetectsQuerySource()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ParameterSourceController)))
                .Returns(new ParameterSourceController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ParameterSource_ExplicitQueryParam");

            // Assert
            Assert.NotNull(tool);
            
            var queryParam = tool.InputSchema.FirstOrDefault(p => p.Name == "filter");
            Assert.NotNull(queryParam);
            Assert.Equal("query", queryParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_FromHeaderAttribute_DetectsHeaderSource()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ParameterSourceController)))
                .Returns(new ParameterSourceController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ParameterSource_ExplicitHeaderParam");

            // Assert
            Assert.NotNull(tool);
            
            var headerParam = tool.InputSchema.FirstOrDefault(p => p.Name == "authorization");
            Assert.NotNull(headerParam);
            Assert.Equal("header", headerParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_ComplexObjectInferred_DetectsBodySource()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ParameterSourceController)))
                .Returns(new ParameterSourceController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ParameterSource_InferredBodyParam");

            // Assert
            Assert.NotNull(tool);
            
            var complexParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(complexParam);
            Assert.Equal("body", complexParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_PrimitiveInferred_DetectsQuerySource()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ParameterSourceController)))
                .Returns(new ParameterSourceController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ParameterSource_InferredQueryParam");

            // Assert
            Assert.NotNull(tool);
            
            var primitiveParam = tool.InputSchema.FirstOrDefault(p => p.Name == "count");
            Assert.NotNull(primitiveParam);
            Assert.Equal("query", primitiveParam.Annotations!["source"]);
        }

        [Fact]
        public void GetToolDefinitions_FromQueryComplexObject_DetectsQuerySource()
        {
            // Arrange - This tests the critical [FromQuery] complex object fix
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ParameterSourceController)))
                .Returns(new ParameterSourceController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ParameterSource_ComplexQueryParam");

            // Assert
            Assert.NotNull(tool);
            
            // Before the fix, complex objects with [FromQuery] were incorrectly classified as "body"
            // After the fix, they should be correctly classified as "query"
            var complexQueryParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(complexQueryParam);
            Assert.Equal("query", complexQueryParam.Annotations!["source"]);
            Assert.Equal("object", complexQueryParam.Type);
        }

        [Fact]
        public void GetToolDefinitions_InheritedProperties_IncludesBaseClassProperties()
        {
            // Arrange - This tests the inheritance chain walking fix
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(InheritanceTestController)))
                .Returns(new InheritanceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "InheritanceTest_ProcessInheritedRequest");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(requestParam);
            Assert.NotNull(requestParam.Properties);
            
            // Before the fix, base class properties were missing
            // After the fix, should include both base and derived properties
            Assert.True(requestParam.Properties.ContainsKey("Provider"), "Should include base class property 'Provider'");
            Assert.True(requestParam.Properties.ContainsKey("ModelName"), "Should include base class property 'ModelName'");
            Assert.True(requestParam.Properties.ContainsKey("PromptVersion"), "Should include base class property 'PromptVersion'");
            
            // Should also include derived properties
            Assert.True(requestParam.Properties.ContainsKey("PromptType"), "Should include derived class property 'PromptType'");
            Assert.True(requestParam.Properties.ContainsKey("TenantId"), "Should include derived class property 'TenantId'");
            
            // Verify required properties include both base and derived
            Assert.NotNull(requestParam.Required);
            Assert.Contains("Provider", requestParam.Required);
            Assert.Contains("ModelName", requestParam.Required);
            Assert.Contains("PromptVersion", requestParam.Required);
            Assert.Contains("PromptType", requestParam.Required);
            Assert.Contains("TenantId", requestParam.Required);
        }

        [Fact]
        public void GetToolDefinitions_FromQueryWithInheritance_BothFixesWorkTogether()
        {
            // Arrange - This tests both fixes working in combination
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(InheritanceTestController)))
                .Returns(new InheritanceTestController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "InheritanceTest_ComplexQueryWithInheritance");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(requestParam);
            
            // Level 1 fix: Should be detected as query source despite being complex object
            Assert.Equal("query", requestParam.Annotations!["source"]);
            Assert.Equal("object", requestParam.Type);
            
            // Level 2 fix: Should include inherited properties
            Assert.NotNull(requestParam.Properties);
            Assert.True(requestParam.Properties.ContainsKey("Provider"), "Should include base class properties");
            Assert.True(requestParam.Properties.ContainsKey("PromptType"), "Should include derived class properties");
        }

        #endregion

        #region MCP Specification Compliance Tests

        [Fact]
        public void GetToolDefinitions_GeneratedSchema_CompliesMcpSpecification()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexObjectController)))
                .Returns(new ComplexObjectController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexObject_ProcessRequest");

            // Assert - Verify MCP tool definition structure
            Assert.NotNull(tool);
            Assert.False(string.IsNullOrEmpty(tool.Name));
            Assert.False(string.IsNullOrEmpty(tool.Description));
            Assert.NotNull(tool.InputSchema);
            
            // Verify each parameter follows MCP parameter schema
            foreach (var param in tool.InputSchema)
            {
                Assert.False(string.IsNullOrEmpty(param.Name));
                Assert.False(string.IsNullOrEmpty(param.Type));
                Assert.True(IsValidMcpType(param.Type));
                
                // Complex objects must have properties
                if (param.Type == "object")
                {
                    Assert.True(param.Annotations?.ContainsKey("properties"));
                }
                
                // Arrays must have items schema
                if (param.Type == "array")
                {
                    Assert.True(param.Annotations?.ContainsKey("items"));
                }
            }
        }

        [Fact]
        public void GetToolDefinitions_JsonSerialization_ProducesValidMcpSchema()
        {
            // Arrange
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexObjectController)))
                .Returns(new ComplexObjectController());

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "ComplexObject_ProcessRequest");

            // Convert to MCP JSON format (simulating what would be sent over MCP protocol)
            var mcpTool = new
            {
                name = tool!.Name,
                description = tool.Description,
                inputSchema = new
                {
                    type = "object",
                    properties = tool.InputSchema.ToDictionary(p => p.Name, p => new
                    {
                        type = p.Type,
                        description = p.Description,
                        required = p.IsRequired
                    }),
                    required = tool.InputSchema.Where(p => p.IsRequired).Select(p => p.Name).ToArray()
                }
            };

            // Assert - Should serialize without errors
            var json = System.Text.Json.JsonSerializer.Serialize(mcpTool);
            Assert.False(string.IsNullOrEmpty(json));
            
            // Should be valid JSON that can be parsed back
            var parsed = System.Text.Json.JsonDocument.Parse(json);
            Assert.True(parsed.RootElement.TryGetProperty("name", out _));
            Assert.True(parsed.RootElement.TryGetProperty("inputSchema", out _));
        }

        private static bool IsValidMcpType(string type)
        {
            var validTypes = new[] { "string", "integer", "number", "boolean", "object", "array", "null" };
            return validTypes.Contains(type);
        }

        #endregion
    }

    #region Test Controller Classes

    [ApiController]
    [Route("api/simple")]
    public class SimpleRouteController : ControllerBase
    {
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            return Ok($"Item {id}");
        }

        [HttpGet("tenant/{tenantId}/customer/{customerId}")]
        public IActionResult GetByTenantAndCustomer(int tenantId, int customerId)
        {
            return Ok($"Tenant {tenantId}, Customer {customerId}");
        }
    }

    [ApiController]
    [Route("api/complex")]
    public class ComplexRouteController : ControllerBase
    {
        [HttpPut("step-definition/{stepDefinitionId}")]
        public IActionResult UpdateStepDefinition(int stepDefinitionId, [FromBody] TestUpdateRequest request)
        {
            return Ok($"Updated step {stepDefinitionId}");
        }

        [HttpGet("items/{id:int:min(1)}")]
        public IActionResult GetWithConstraints(int id)
        {
            return Ok($"Item {id}");
        }
    }

    [ApiController]
    [Route("api/complex-objects")]
    public class ComplexObjectController : ControllerBase
    {
        [HttpPost("process")]
        public IActionResult ProcessRequest([FromBody] TestUpdateRequest request)
        {
            return Ok("Processed");
        }

        [HttpPost("process-enum")]
        public IActionResult ProcessEnumRequest([FromBody] TestEnumRequest request)
        {
            return Ok("Processed enum");
        }

        [HttpPost("process-batch")]
        public IActionResult ProcessBatchRequest([FromBody] TestBatchRequest request)
        {
            return Ok("Processed batch");
        }
    }

    [ApiController]
    [Route("api/parameter-sources")]
    public class ParameterSourceController : ControllerBase
    {
        [HttpPost("explicit-body")]
        public IActionResult ExplicitBodyParam([FromBody] TestUpdateRequest request)
        {
            return Ok("Body param");
        }

        [HttpGet("explicit-query")]
        public IActionResult ExplicitQueryParam([FromQuery] string filter)
        {
            return Ok("Query param");
        }

        [HttpGet("explicit-header")]
        public IActionResult ExplicitHeaderParam([FromHeader] string authorization)
        {
            return Ok("Header param");
        }

        [HttpPost("inferred-body")]
        public IActionResult InferredBodyParam(TestUpdateRequest request)
        {
            return Ok("Inferred body");
        }

        [HttpGet("inferred-query")]
        public IActionResult InferredQueryParam(int count)
        {
            return Ok("Inferred query");
        }

        [HttpGet("complex-query")]
        public IActionResult ComplexQueryParam([FromQuery] TestUpdateRequest request)
        {
            return Ok("Complex query param");
        }
    }

    [ApiController]
    [Route("api/inheritance-test")]
    public class InheritanceTestController : ControllerBase
    {
        [HttpPost("process-inherited")]
        public IActionResult ProcessInheritedRequest([FromBody] ExtendedTestRequest request)
        {
            return Ok("Processed inherited request");
        }

        [HttpGet("complex-query-inheritance")]
        public IActionResult ComplexQueryWithInheritance([FromQuery] ExtendedTestRequest request)
        {
            return Ok("Complex query with inheritance");
        }
    }

    #endregion

    #region Test Model Classes

    public class TestUpdateRequest
    {
        public string StepCode { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TestComposableStepDefinition Definition { get; set; } = new();
    }

    public class TestComposableStepDefinition
    {
        public string StepCode { get; set; } = string.Empty;
        public TestProcessor Processor { get; set; } = new();
        public List<string> StorageHandlers { get; set; } = new();
    }

    public class TestProcessor
    {
        public string Type { get; set; } = string.Empty;
        public List<string> Providers { get; set; } = new();
    }

    public class TestEnumRequest
    {
        public string Name { get; set; } = string.Empty;
        public TestStatus Status { get; set; }
    }

    public enum TestStatus
    {
        Active,
        Inactive,
        Pending
    }

    public class TestBatchRequest
    {
        public string BatchName { get; set; } = string.Empty;
        public List<TestUpdateRequest> Items { get; set; } = new();
    }

    // Test models for inheritance chain testing (mirrors LlmProviderModelRequest hierarchy)
    public class BaseTestRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Provider { get; set; } = string.Empty;
        
        [System.ComponentModel.DataAnnotations.Required]
        public string ModelName { get; set; } = string.Empty;
        
        [System.ComponentModel.DataAnnotations.Required]
        public string PromptVersion { get; set; } = string.Empty;
    }

    public class ExtendedTestRequest : BaseTestRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string PromptType { get; set; } = string.Empty;
        
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int TenantId { get; set; }
    }

    #endregion
}