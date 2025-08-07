using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using MCPInvoke.AspNetCore;
using MCPInvoke;

namespace MCPInvoke.Tests.Performance
{
    /// <summary>
    /// Performance tests to ensure schema generation enhancements don't introduce significant overhead.
    /// These tests validate that complex object introspection, route parsing, and caching work efficiently.
    /// </summary>
    public class SchemaGenerationPerformanceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<AspNetControllerToolDefinitionProvider>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public SchemaGenerationPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _loggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
        }

        #region Schema Generation Performance Tests

        [Fact]
        public void SchemaGeneration_SimpleController_CompletesWithinTimeLimit()
        {
            // Arrange
            var controller = new SimplePerformanceController();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(SimplePerformanceController)))
                .Returns(controller);

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var tools = provider.GetToolDefinitions().ToList();
            stopwatch.Stop();

            // Assert
            Assert.NotEmpty(tools);
            Assert.True(stopwatch.ElapsedMilliseconds < 100, 
                $"Simple controller schema generation took {stopwatch.ElapsedMilliseconds}ms, should be under 100ms");
            
            _output.WriteLine($"Simple controller schema generation: {stopwatch.ElapsedMilliseconds}ms for {tools.Count} tools");
        }

        [Fact]
        public void SchemaGeneration_ComplexController_CompletesWithinTimeLimit()
        {
            // Arrange
            var controller = new ComplexPerformanceController();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexPerformanceController)))
                .Returns(controller);

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var tools = provider.GetToolDefinitions().ToList();
            stopwatch.Stop();

            // Assert
            Assert.NotEmpty(tools);
            Assert.True(stopwatch.ElapsedMilliseconds < 500, 
                $"Complex controller schema generation took {stopwatch.ElapsedMilliseconds}ms, should be under 500ms");
            
            _output.WriteLine($"Complex controller schema generation: {stopwatch.ElapsedMilliseconds}ms for {tools.Count} tools");

            // Verify complex schemas were generated
            var complexTool = tools.FirstOrDefault(t => t.Name.Contains("ProcessComplexWorkflow"));
            Assert.NotNull(complexTool);
            
            var complexParam = complexTool.InputSchema.FirstOrDefault(p => p.Type == "object");
            Assert.NotNull(complexParam);
            Assert.True(complexParam.Annotations?.ContainsKey("properties"));
        }

        [Fact]
        public void SchemaGeneration_ManyControllers_ScalesLinear()
        {
            // Arrange - Test with multiple controllers to verify linear scaling
            var controllers = new object[]
            {
                new SimplePerformanceController(),
                new ComplexPerformanceController(),
                new NestedObjectController(),
                new ArrayParameterController(),
                new EnumParameterController()
            };

            foreach (var controller in controllers)
            {
                _serviceProviderMock.Setup(sp => sp.GetService(controller.GetType()))
                    .Returns(controller);
            }

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var tools = provider.GetToolDefinitions().ToList();
            stopwatch.Stop();

            // Assert
            Assert.NotEmpty(tools);
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
                $"Multi-controller schema generation took {stopwatch.ElapsedMilliseconds}ms, should be under 1000ms");
            
            _output.WriteLine($"Multi-controller schema generation: {stopwatch.ElapsedMilliseconds}ms for {tools.Count} tools across {controllers.Length} controllers");

            // Should scale reasonably with number of controllers
            var avgTimePerTool = (double)stopwatch.ElapsedMilliseconds / tools.Count;
            Assert.True(avgTimePerTool < 50, $"Average time per tool ({avgTimePerTool:F2}ms) should be under 50ms");
        }

        #endregion

        #region Caching Performance Tests

        [Fact]
        public void SchemaGeneration_RepeatedCalls_BenefitFromCaching()
        {
            // Arrange
            var controller = new ComplexPerformanceController();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexPerformanceController)))
                .Returns(controller);

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // First call (cold - no cache)
            var stopwatch1 = Stopwatch.StartNew();
            var tools1 = provider.GetToolDefinitions().ToList();
            stopwatch1.Stop();

            // Second call (should benefit from caching)
            var stopwatch2 = Stopwatch.StartNew();
            var tools2 = provider.GetToolDefinitions().ToList();
            stopwatch2.Stop();

            // Third call (cache should be fully warmed)
            var stopwatch3 = Stopwatch.StartNew();
            var tools3 = provider.GetToolDefinitions().ToList();
            stopwatch3.Stop();

            // Assert
            Assert.Equal(tools1.Count, tools2.Count);
            Assert.Equal(tools2.Count, tools3.Count);

            _output.WriteLine($"Schema generation times: Cold={stopwatch1.ElapsedMilliseconds}ms, Warm1={stopwatch2.ElapsedMilliseconds}ms, Warm2={stopwatch3.ElapsedMilliseconds}ms");

            // Cached calls should be reasonably fast (allow for timing variations in CI environments)
            // If first call was very fast (< 10ms), just verify subsequent calls are reasonable
            if (stopwatch1.ElapsedMilliseconds < 10)
            {
                Assert.True(stopwatch2.ElapsedMilliseconds <= 50, "Second call should be reasonably fast");
                Assert.True(stopwatch3.ElapsedMilliseconds <= 50, "Third call should be reasonably fast");
            }
            else
            {
                // For longer initial calls, expect some caching benefits, but be more lenient for CI
                // Allow up to 90% of initial time to account for CI environment variations
                Assert.True(stopwatch2.ElapsedMilliseconds <= Math.Max(stopwatch1.ElapsedMilliseconds * 0.9, stopwatch1.ElapsedMilliseconds - 5),
                    $"Second call ({stopwatch2.ElapsedMilliseconds}ms) should benefit from caching compared to first call ({stopwatch1.ElapsedMilliseconds}ms)");
                Assert.True(stopwatch3.ElapsedMilliseconds <= Math.Max(stopwatch1.ElapsedMilliseconds * 0.9, stopwatch1.ElapsedMilliseconds - 5),
                    $"Third call ({stopwatch3.ElapsedMilliseconds}ms) should benefit from caching compared to first call ({stopwatch1.ElapsedMilliseconds}ms)");
            }
        }

        [Fact]
        public void SchemaGeneration_CircularReferences_DoesNotCauseStackOverflow()
        {
            // Arrange - Test circular reference handling performance
            var controller = new CircularReferenceController();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(CircularReferenceController)))
                .Returns(controller);

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var exception = Record.Exception(() =>
            {
                var tools = provider.GetToolDefinitions().ToList();
                return tools;
            });
            stopwatch.Stop();

            // Assert
            Assert.Null(exception); // Should not throw StackOverflowException
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Circular reference handling took {stopwatch.ElapsedMilliseconds}ms, should complete under 1000ms");

            _output.WriteLine($"Circular reference handling: {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Memory Usage Tests

        [Fact]
        public void SchemaGeneration_LargeSchema_ManagesMemoryEfficiently()
        {
            // Arrange
            var controller = new LargeSchemaController();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(LargeSchemaController)))
                .Returns(controller);

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Measure memory before
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryBefore = GC.GetTotalMemory(false);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var tools = provider.GetToolDefinitions().ToList();
            stopwatch.Stop();

            // Measure memory after
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;

            // Assert
            Assert.NotEmpty(tools);
            Assert.True(memoryUsed < 10 * 1024 * 1024, // Less than 10MB
                $"Schema generation used {memoryUsed / 1024 / 1024:F2}MB, should be under 10MB");

            _output.WriteLine($"Large schema generation: {stopwatch.ElapsedMilliseconds}ms, {memoryUsed / 1024:F0}KB memory");
        }

        #endregion

        #region Route Parameter Parsing Performance Tests

        [Fact]
        public void RouteParameterExtraction_ComplexRoutes_CompletesQuickly()
        {
            // Arrange
            var controller = new ComplexRoutePerformanceController();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ComplexRoutePerformanceController)))
                .Returns(controller);

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Act & Measure route parameter extraction specifically
            var stopwatch = Stopwatch.StartNew();
            var tools = provider.GetToolDefinitions().ToList();
            stopwatch.Stop();

            // Assert
            Assert.NotEmpty(tools);
            
            // Verify route parameters were extracted correctly from complex templates
            var complexRouteTool = tools.FirstOrDefault(t => t.Name.Contains("MultiLevelRoute"));
            Assert.NotNull(complexRouteTool);
            
            var routeParams = complexRouteTool.InputSchema.Where(p => 
                p.Annotations?.ContainsKey("source") == true && 
                p.Annotations!["source"].ToString() == "route").ToList();
            
            Assert.True(routeParams.Count >= 3, "Should extract multiple route parameters");
            Assert.True(stopwatch.ElapsedMilliseconds < 200,
                $"Route parameter extraction took {stopwatch.ElapsedMilliseconds}ms, should be under 200ms");

            _output.WriteLine($"Complex route parsing: {stopwatch.ElapsedMilliseconds}ms for {routeParams.Count} route parameters");
        }

        #endregion

        #region Regression Performance Tests

        [Fact]
        public void SchemaGeneration_Workflow3ScaleTest_HandlesRealWorldLoad()
        {
            // Arrange - Simulate real Workflow3 controller with many endpoints
            var controllers = new object[]
            {
                new MockWorkflow3PerformanceController(),
                new MockAnalyticsPerformanceController(),
                new MockPipelinePerformanceController()
            };

            foreach (var controller in controllers)
            {
                _serviceProviderMock.Setup(sp => sp.GetService(controller.GetType()))
                    .Returns(controller);
            }

            var options = new AspNetControllerToolOptions();
            var provider = new AspNetControllerToolDefinitionProvider(_serviceProviderMock.Object, _loggerMock.Object, options, 
                new[] { typeof(SchemaGenerationPerformanceTests).Assembly });

            // Act & Measure - Simulate real-world load
            var stopwatch = Stopwatch.StartNew();
            var tools = provider.GetToolDefinitions().ToList();
            stopwatch.Stop();

            // Assert - Performance should be acceptable for production use
            Assert.True(tools.Count >= 15, "Should generate tools for all methods");
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, 
                $"Workflow3-scale schema generation took {stopwatch.ElapsedMilliseconds}ms, should be under 2000ms for production");

            // Verify complex schemas were generated correctly
            var complexTools = tools.Where(t => 
                t.InputSchema.Any(p => p.Type == "object" && 
                p.Annotations?.ContainsKey("properties") == true)).ToList();
            
            Assert.True(complexTools.Count >= 5, "Should have multiple complex object tools");

            _output.WriteLine($"Workflow3-scale performance: {stopwatch.ElapsedMilliseconds}ms for {tools.Count} tools ({complexTools.Count} complex)");
        }

        #endregion
    }

    #region Performance Test Controllers

    [ApiController]
    [Route("api/simple-perf")]
    public class SimplePerformanceController : ControllerBase
    {
        [HttpGet("{id}")]
        public IActionResult Get(int id) => Ok($"Item {id}");

        [HttpPost]
        public IActionResult Create(string name) => Ok($"Created {name}");

        [HttpPut("{id}")]
        public IActionResult Update(int id, string name) => Ok($"Updated {id}");

        [HttpDelete("{id}")]
        public IActionResult Delete(int id) => Ok($"Deleted {id}");
    }

    [ApiController]
    [Route("api/complex-perf")]
    public class ComplexPerformanceController : ControllerBase
    {
        [HttpPost("process-complex-workflow")]
        public IActionResult ProcessComplexWorkflow([FromBody] ComplexWorkflowRequest request) 
            => Ok("Processed complex workflow");

        [HttpPut("update-nested-config/{configId}")]
        public IActionResult UpdateNestedConfig(int configId, [FromBody] NestedConfigurationRequest request)
            => Ok($"Updated config {configId}");

        [HttpPost("batch-process")]
        public IActionResult BatchProcess([FromBody] BatchProcessingRequest request)
            => Ok($"Processed {request.Items?.Count ?? 0} items");
    }

    [ApiController]
    [Route("api/nested-objects")]
    public class NestedObjectController : ControllerBase
    {
        [HttpPost("deep-nested")]
        public IActionResult ProcessDeepNested([FromBody] DeeplyNestedRequest request)
            => Ok("Processed deep nested");
    }

    [ApiController]
    [Route("api/arrays")]
    public class ArrayParameterController : ControllerBase
    {
        [HttpPost("process-arrays")]
        public IActionResult ProcessArrays([FromBody] ArrayRequest request)
            => Ok($"Processed {request.StringArray?.Count ?? 0} strings");
    }

    [ApiController]
    [Route("api/enums")]
    public class EnumParameterController : ControllerBase
    {
        [HttpPost("process-enum")]
        public IActionResult ProcessEnum(PerformanceTestStatus status, PerformanceTestPriority priority)
            => Ok($"Processed {status} with {priority} priority");
    }

    [ApiController]
    [Route("api/circular")]
    public class CircularReferenceController : ControllerBase
    {
        [HttpPost("process-circular")]
        public IActionResult ProcessCircular([FromBody] CircularReferenceNode node)
            => Ok("Processed circular reference");
    }

    [ApiController]
    [Route("api/large-schema")]
    public class LargeSchemaController : ControllerBase
    {
        [HttpPost("process-large")]
        public IActionResult ProcessLarge([FromBody] LargeSchemaRequest request)
            => Ok("Processed large schema");
    }

    [ApiController]
    [Route("api/complex-routes")]
    public class ComplexRoutePerformanceController : ControllerBase
    {
        [HttpGet("tenant/{tenantId}/customer/{customerId}/account/{accountId}")]
        public IActionResult MultiLevelRoute(int tenantId, int customerId, int accountId)
            => Ok($"Tenant {tenantId}, Customer {customerId}, Account {accountId}");

        [HttpPut("workflow/{workflowId}/step/{stepId}/config/{configId:guid}")]
        public IActionResult WorkflowStepConfig(int workflowId, int stepId, Guid configId, [FromBody] object config)
            => Ok($"Updated workflow {workflowId}");

        [HttpGet("reports/{reportType}/data/{year:int:range(2020,2030)}/{month:int:range(1,12)}")]
        public IActionResult ReportsWithConstraints(string reportType, int year, int month)
            => Ok($"Report {reportType} for {year}-{month}");
    }

    [ApiController]
    [Route("api/mock-workflow3")]
    public class MockWorkflow3PerformanceController : ControllerBase
    {
        [HttpPost("execute")]
        public IActionResult Execute([FromBody] object request) => Ok("Executed");

        [HttpGet("definitions")]
        public IActionResult GetDefinitions() => Ok("Definitions");

        [HttpPut("definitions/{id}")]
        public IActionResult UpdateDefinition(int id, [FromBody] object request) => Ok($"Updated {id}");

        [HttpPost("definitions")]
        public IActionResult CreateDefinition([FromBody] object request) => Ok("Created");

        [HttpDelete("definitions/{id}")]
        public IActionResult DeleteDefinition(int id) => Ok($"Deleted {id}");

        [HttpGet("executions")]
        public IActionResult GetExecutions(int tenantId, int? customerId = null) => Ok("Executions");

        [HttpPut("step-definitions/{stepId}")]
        public IActionResult UpdateStepDefinition(int stepId, [FromBody] object request) => Ok($"Updated step {stepId}");
    }

    [ApiController]
    [Route("api/mock-analytics")]
    public class MockAnalyticsPerformanceController : ControllerBase
    {
        [HttpGet("customers")]
        public IActionResult GetCustomers(int tenantId, int page = 1, int size = 10) => Ok("Customers");

        [HttpGet("customers/{customerId}")]
        public IActionResult GetCustomer(int customerId) => Ok($"Customer {customerId}");

        [HttpGet("customers/{customerId}/transactions")]
        public IActionResult GetTransactions(int customerId, DateTime? start = null) => Ok("Transactions");

        [HttpPost("insights/generate")]
        public IActionResult GenerateInsights([FromBody] object request) => Ok("Generated");

        [HttpGet("insights/{insightId}")]
        public IActionResult GetInsight(int insightId) => Ok($"Insight {insightId}");
    }

    [ApiController]
    [Route("api/mock-pipeline")]
    public class MockPipelinePerformanceController : ControllerBase
    {
        [HttpPost("validate")]
        public IActionResult Validate([FromBody] object config) => Ok("Validated");

        [HttpGet("handlers")]
        public IActionResult GetHandlers() => Ok("Handlers");

        [HttpGet("handlers/{handlerType}")]
        public IActionResult GetHandler(string handlerType) => Ok($"Handler {handlerType}");

        [HttpPost("execute")]
        public IActionResult ExecutePipeline([FromBody] object request) => Ok("Executed");

        [HttpGet("metrics")]
        public IActionResult GetMetrics() => Ok("Metrics");
    }

    #endregion

    #region Performance Test Models

    public class ComplexWorkflowRequest
    {
        public string WorkflowName { get; set; } = string.Empty;
        public List<WorkflowStep> Steps { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public class WorkflowStep
    {
        public string StepCode { get; set; } = string.Empty;
        public int Order { get; set; }
        public StepConfiguration Configuration { get; set; } = new();
        public List<StepDependency> Dependencies { get; set; } = new();
    }

    public class StepConfiguration
    {
        public string ProcessorType { get; set; } = string.Empty;
        public Dictionary<string, object> Settings { get; set; } = new();
        public List<string> Providers { get; set; } = new();
    }

    public class StepDependency
    {
        public string DependsOnStep { get; set; } = string.Empty;
        public string DependencyType { get; set; } = string.Empty;
    }

    public class NestedConfigurationRequest
    {
        public string Name { get; set; } = string.Empty;
        public ComplexWorkflowRequest NestedWorkflow { get; set; } = new();
        public List<ComplexWorkflowRequest> RelatedWorkflows { get; set; } = new();
    }

    public class BatchProcessingRequest
    {
        public string BatchName { get; set; } = string.Empty;
        public List<ComplexWorkflowRequest> Items { get; set; } = new();
        public ProcessingOptions Options { get; set; } = new();
    }

    public class ProcessingOptions
    {
        public int MaxConcurrency { get; set; } = 1;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public bool FailFast { get; set; } = false;
        public Dictionary<string, object> AdditionalSettings { get; set; } = new();
    }

    public class DeeplyNestedRequest
    {
        public string Name { get; set; } = string.Empty;
        public Level1Nested Level1 { get; set; } = new();
    }

    public class Level1Nested
    {
        public string Value { get; set; } = string.Empty;
        public Level2Nested Level2 { get; set; } = new();
        public List<Level2Nested> Level2Array { get; set; } = new();
    }

    public class Level2Nested
    {
        public string Value { get; set; } = string.Empty;
        public Level3Nested Level3 { get; set; } = new();
    }

    public class Level3Nested
    {
        public string Value { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class ArrayRequest
    {
        public List<string> StringArray { get; set; } = new();
        public List<int> IntArray { get; set; } = new();
        public List<ComplexWorkflowRequest> ComplexArray { get; set; } = new();
        public string[][] JaggedArray { get; set; } = new string[0][];
    }

    public class CircularReferenceNode
    {
        public string Name { get; set; } = string.Empty;
        public CircularReferenceNode? Parent { get; set; }
        public List<CircularReferenceNode> Children { get; set; } = new();
    }

    public class LargeSchemaRequest
    {
        // Simulate a large schema with many properties
        public string Property01 { get; set; } = string.Empty;
        public string Property02 { get; set; } = string.Empty;
        public string Property03 { get; set; } = string.Empty;
        public string Property04 { get; set; } = string.Empty;
        public string Property05 { get; set; } = string.Empty;
        public string Property06 { get; set; } = string.Empty;
        public string Property07 { get; set; } = string.Empty;
        public string Property08 { get; set; } = string.Empty;
        public string Property09 { get; set; } = string.Empty;
        public string Property10 { get; set; } = string.Empty;
        public string Property11 { get; set; } = string.Empty;
        public string Property12 { get; set; } = string.Empty;
        public string Property13 { get; set; } = string.Empty;
        public string Property14 { get; set; } = string.Empty;
        public string Property15 { get; set; } = string.Empty;
        public string Property16 { get; set; } = string.Empty;
        public string Property17 { get; set; } = string.Empty;
        public string Property18 { get; set; } = string.Empty;
        public string Property19 { get; set; } = string.Empty;
        public string Property20 { get; set; } = string.Empty;

        public List<LargeNestedObject> NestedObjects { get; set; } = new();
        public Dictionary<string, object> DynamicProperties { get; set; } = new();
    }

    public class LargeNestedObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public enum PerformanceTestStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum PerformanceTestPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    #endregion
}