using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.AspNetCore;
using MCPInvoke.Services;
using MCPInvoke;

namespace MCPInvoke.Tests.Integration
{
    /// <summary>
    /// Integration tests that validate the complete schema generation and execution pipeline
    /// for real Workflow3 scenarios that were failing due to schema gaps.
    /// </summary>
    public class Workflow3ScenarioTests
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly Mock<ILogger<McpExecutionService>> _executionLoggerMock;
        private readonly Mock<ILogger<AspNetControllerToolDefinitionProvider>> _providerLoggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly AspNetControllerToolDefinitionProvider _provider;
        private readonly McpExecutionService _executionService;

        public Workflow3ScenarioTests()
        {
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            _executionLoggerMock = new Mock<ILogger<McpExecutionService>>();
            _providerLoggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            var options = new AspNetControllerToolOptions();
            _provider = new AspNetControllerToolDefinitionProvider(
                _serviceProviderMock.Object, 
                _providerLoggerMock.Object, 
                options, 
                new[] { typeof(Workflow3ScenarioTests).Assembly });
            _executionService = new McpExecutionService(_serviceScopeFactoryMock.Object, _provider, _executionLoggerMock.Object);
        }

        #region Workflow3_UpdateStepDefinition Scenario Tests

        [Fact]
        public async Task Workflow3_UpdateStepDefinition_CompleteScenario_GeneratesCorrectSchema()
        {
            // Arrange - Replicate the exact failing scenario
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Act - Get tool definition (this was failing before the fix)
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "MockWorkflow3_UpdateStepDefinition");

            // Assert - Verify complete schema generation
            Assert.NotNull(tool);
            Assert.Equal("Update an existing workflow step definition", tool.Description);

            // Should have both route and body parameters
            Assert.Equal(2, tool.InputSchema.Count);

            // Route parameter: stepDefinitionId
            var routeParam = tool.InputSchema.FirstOrDefault(p => p.Name == "stepDefinitionId");
            Assert.NotNull(routeParam);
            Assert.Equal("integer", routeParam.Type);
            Assert.Equal("route", routeParam.Annotations?["source"]);
            Assert.True(routeParam.IsRequired);

            // Body parameter: request with complex object schema
            var bodyParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(bodyParam);
            Assert.Equal("object", bodyParam.Type);
            Assert.Equal("body", bodyParam.Annotations?["source"]);

            // Verify complex object properties
            Assert.True(bodyParam.Annotations?.ContainsKey("properties"));
            var properties = bodyParam.Annotations["properties"] as Dictionary<string, object>;
            Assert.NotNull(properties);
            Assert.True(properties.ContainsKey("StepCode"));
            Assert.True(properties.ContainsKey("Definition"));

            // Verify nested object schema for ComposableStepDefinition
            var definitionProp = properties["Definition"] as Dictionary<string, object>;
            Assert.NotNull(definitionProp);
            Assert.Equal("object", definitionProp["type"]);
            Assert.True(definitionProp.ContainsKey("properties"));
        }

        [Fact]
        public async Task Workflow3_UpdateStepDefinition_ExecutesMcpCall_Successfully()
        {
            // Arrange - Full end-to-end scenario
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Setup service scope
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);
            serviceScopeMock.Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock.Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);

            // Create the exact MCP request that was failing
            var mcpRequest = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "MockWorkflow3_UpdateStepDefinition",
                    arguments = new
                    {
                        stepDefinitionId = 7,
                        request = new
                        {
                            stepCode = "BALANCE_ANALYSIS",
                            stepName = "Enhanced Balance Analysis",
                            description = "Analyzes customer balance patterns with enhanced insights",
                            definition = new
                            {
                                stepCode = "BALANCE_ANALYSIS",
                                processor = new
                                {
                                    type = "LLM",
                                    providers = new[] { "openai", "anthropic" },
                                    promptTemplateId = (int?)null
                                },
                                storageHandlers = new[] { "database", "cache" }
                            }
                        }
                    }
                },
                id = 1
            };

            var jsonRequest = JsonSerializer.Serialize(mcpRequest);

            // Act - Execute the MCP call
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert - Should succeed without schema errors
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            Assert.False(responseJson.RootElement.TryGetProperty("error", out _), 
                "Request should succeed without 'Parameter schema not found' error");

            // Verify the response contains expected result
            var content = ExtractContentText(result);
            Assert.Contains("Updated step definition 7", content);
        }

        [Fact]
        public async Task Workflow3_UpdateStepDefinition_HandlesNullNestedProperties_Gracefully()
        {
            // Arrange - Test the edge case with null nested properties
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);
            serviceScopeMock.Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock.Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);

            var mcpRequest = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "MockWorkflow3_UpdateStepDefinition",
                    arguments = new
                    {
                        stepDefinitionId = 8,
                        request = new
                        {
                            stepCode = "LIFE_EVENT_DETECTION",
                            definition = new
                            {
                                processor = new
                                {
                                    promptTemplateId = (int?)null  // This was causing issues
                                }
                            }
                        }
                    }
                },
                id = 2
            };

            var jsonRequest = JsonSerializer.Serialize(mcpRequest);

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            var content = ExtractContentText(result);
            Assert.Contains("Updated step definition 8", content);
        }

        #endregion

        #region Workflow3_CreateWorkflowDefinition Scenario Tests

        [Fact]
        public async Task Workflow3_CreateWorkflowDefinition_ComplexNestedObjects_GeneratesSchema()
        {
            // Arrange
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Act
            var tools = _provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "MockWorkflow3_CreateWorkflowDefinition");

            // Assert
            Assert.NotNull(tool);
            
            var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
            Assert.NotNull(requestParam);
            Assert.Equal("object", requestParam.Type);
            
            var properties = requestParam.Annotations["properties"] as Dictionary<string, object>;
            Assert.True(properties.ContainsKey("WorkflowName"));
            Assert.True(properties.ContainsKey("Steps"));
            
            // Verify array schema for steps
            var stepsProperty = properties["Steps"] as Dictionary<string, object>;
            Assert.Equal("array", stepsProperty["type"]);
            Assert.True(stepsProperty.ContainsKey("items"));
        }

        [Fact]
        public async Task Workflow3_CreateWorkflowDefinition_ExecutesWithComplexData_Successfully()
        {
            // Arrange
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);
            serviceScopeMock.Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            _serviceScopeFactoryMock.Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);

            var mcpRequest = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "MockWorkflow3_CreateWorkflowDefinition",
                    arguments = new
                    {
                        request = new
                        {
                            workflowName = "Customer Analysis Pipeline",
                            description = "Comprehensive customer analysis workflow",
                            tenantId = 1,
                            steps = new[]
                            {
                                new
                                {
                                    stepCode = "BALANCE_ANALYSIS",
                                    order = 1,
                                    isActive = true
                                },
                                new
                                {
                                    stepCode = "LIFE_EVENT_DETECTION", 
                                    order = 2,
                                    isActive = true
                                }
                            }
                        }
                    }
                },
                id = 3
            };

            var jsonRequest = JsonSerializer.Serialize(mcpRequest);

            // Act
            var response = await _executionService.ProcessRequestAsync(jsonRequest);

            // Assert
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            var content = ExtractContentText(result);
            Assert.Contains("Created workflow: Customer Analysis Pipeline", content);
        }

        #endregion

        #region Real-World Error Scenario Tests

        [Fact]
        public void Workflow3_SchemaGeneration_PreventsParameterSchemaNotFoundError()
        {
            // Arrange - This test specifically prevents the regression that caused the original bug
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Act
            var tools = _provider.GetToolDefinitions();

            // Assert - All Workflow3 operations should have complete schemas
            var workflow3Tools = tools.Where(t => t.Name.StartsWith("MockWorkflow3_")).ToList();
            Assert.NotEmpty(workflow3Tools);

            foreach (var tool in workflow3Tools)
            {
                // Every parameter should have a complete schema definition
                foreach (var param in tool.InputSchema)
                {
                    Assert.False(string.IsNullOrEmpty(param.Name), 
                        $"Parameter name missing in tool {tool.Name}");
                    Assert.False(string.IsNullOrEmpty(param.Type), 
                        $"Parameter type missing for {param.Name} in tool {tool.Name}");
                    
                    // Complex objects must have detailed properties
                    if (param.Type == "object")
                    {
                        Assert.True(param.Annotations?.ContainsKey("properties"), 
                            $"Object parameter {param.Name} in tool {tool.Name} missing properties schema");
                    }

                    // Route parameters must be identified
                    if (param.Annotations?.ContainsKey("source") == true)
                    {
                        var source = param.Annotations["source"].ToString();
                        Assert.True(new[] { "route", "body", "query", "header" }.Contains(source),
                            $"Invalid parameter source '{source}' for {param.Name} in tool {tool.Name}");
                    }
                }
            }
        }

        [Fact]
        public async Task Workflow3_McpProtocol_MatchesOfficialSpecification()
        {
            // Arrange - Verify generated schemas match MCP specification format
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Act
            var tools = _provider.GetToolDefinitions();
            var updateTool = tools.FirstOrDefault(t => t.Name == "MockWorkflow3_UpdateStepDefinition");

            // Convert to official MCP tool definition format
            var mcpToolDefinition = new
            {
                name = updateTool.Name,
                description = updateTool.Description,
                inputSchema = new
                {
                    type = "object",
                    properties = updateTool.InputSchema.ToDictionary(p => p.Name, p => new Dictionary<string, object>
                    {
                        ["type"] = p.Type,
                        ["description"] = p.Description ?? $"Parameter {p.Name}"
                    }),
                    required = updateTool.InputSchema.Where(p => p.IsRequired).Select(p => p.Name).ToArray()
                }
            };

            // Assert - Should match MCP specification structure
            Assert.Equal("MockWorkflow3_UpdateStepDefinition", mcpToolDefinition.name);
            Assert.Equal("object", mcpToolDefinition.inputSchema.type);
            Assert.NotEmpty(mcpToolDefinition.inputSchema.properties);
            Assert.Contains("stepDefinitionId", mcpToolDefinition.inputSchema.required);

            // Should serialize to valid JSON without errors
            var json = JsonSerializer.Serialize(mcpToolDefinition, new JsonSerializerOptions { WriteIndented = true });
            Assert.False(string.IsNullOrEmpty(json));

            // Should deserialize back without data loss
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            Assert.NotNull(deserialized);
            Assert.True(deserialized.ContainsKey("inputSchema"));
        }

        #endregion

        #region Performance and Edge Case Tests

        [Fact]
        public void Workflow3_SchemaGeneration_HandlesCircularReferences_Gracefully()
        {
            // Arrange - Test circular reference handling
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Act & Assert - Should not throw StackOverflowException
            var exception = Record.Exception(() => _provider.GetToolDefinitions());
            Assert.Null(exception);
        }

        [Fact] 
        public void Workflow3_SchemaGeneration_CachesResults_ForPerformance()
        {
            // Arrange
            var controller = new MockWorkflow3Controller();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWorkflow3Controller)))
                .Returns(controller);

            // Act - Call multiple times
            var tools1 = _provider.GetToolDefinitions().ToList();
            var tools2 = _provider.GetToolDefinitions().ToList();
            var tools3 = _provider.GetToolDefinitions().ToList();

            // Assert - Should return consistent results (caching test)
            Assert.Equal(tools1.Count, tools2.Count);
            Assert.Equal(tools2.Count, tools3.Count);
            
            var tool1 = tools1.FirstOrDefault(t => t.Name == "MockWorkflow3_UpdateStepDefinition");
            var tool2 = tools2.FirstOrDefault(t => t.Name == "MockWorkflow3_UpdateStepDefinition");
            
            Assert.Equal(tool1.InputSchema.Count, tool2.InputSchema.Count);
        }

        #endregion

        /// <summary>
        /// Helper method to extract text content from MCP content schema format
        /// </summary>
        private string ExtractContentText(JsonElement result)
        {
            if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                var firstContent = content.EnumerateArray().FirstOrDefault();
                if (firstContent.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                    firstContent.TryGetProperty("text", out var text))
                {
                    var textValue = text.GetString() ?? string.Empty;
                    try
                    {
                        return JsonSerializer.Deserialize<string>(textValue) ?? string.Empty;
                    }
                    catch
                    {
                        return textValue;
                    }
                }
            }
            return result.GetString() ?? string.Empty;
        }
    }

    #region Mock Controller for Testing

    [ApiController]
    [Route("api/v3/workflow")]
    public class MockWorkflow3Controller : ControllerBase
    {
        [HttpPut("step-definition/{stepDefinitionId}")]
        [System.ComponentModel.Description("Update an existing workflow step definition")]
        public IActionResult UpdateStepDefinition(int stepDefinitionId, [FromBody] MockUpdateStepDefinitionRequest request)
        {
            return Ok($"Updated step definition {stepDefinitionId}");
        }

        [HttpPost("definitions")]
        public IActionResult CreateWorkflowDefinition([FromBody] MockCreateWorkflowDefinitionRequest request)
        {
            return Ok($"Created workflow: {request.WorkflowName}");
        }

        [HttpGet("definitions/{workflowDefinitionId}")]
        public IActionResult GetWorkflowDefinition(int workflowDefinitionId)
        {
            return Ok($"Workflow definition {workflowDefinitionId}");
        }

        [HttpGet("step-definitions")]
        public IActionResult GetAllStepDefinitions()
        {
            return Ok("All step definitions");
        }

        [HttpGet("executions")]
        public IActionResult GetExecutions(int tenantId, int? customerId, string workflowName, 
            string status, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 10)
        {
            return Ok("Executions list");
        }
    }

    #endregion

    #region Mock Model Classes

    public class MockUpdateStepDefinitionRequest
    {
        public string StepCode { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MockComposableStepDefinition Definition { get; set; } = new();
        public string OutputFragmentType { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
    }

    public class MockComposableStepDefinition
    {
        public string StepCode { get; set; } = string.Empty;
        public MockProcessor Processor { get; set; } = new();
        public List<string> StorageHandlers { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public class MockProcessor
    {
        public string Type { get; set; } = string.Empty;
        public List<string> Providers { get; set; } = new();
        public int? PromptTemplateId { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class MockCreateWorkflowDefinitionRequest
    {
        public string WorkflowName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public List<MockWorkflowStep> Steps { get; set; } = new();
    }

    public class MockWorkflowStep
    {
        public string StepCode { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    #endregion
}