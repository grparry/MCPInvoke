using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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
using MCPInvoke.Tests.Patterns;

namespace MCPInvoke.Tests.Patterns
{
    /// <summary>
    /// Demonstration tests showing how to apply generic web API test patterns
    /// to common business domain endpoint types.
    /// 
    /// Based on proven MCPInvoke v2.0 Enhanced Parameter Binding success patterns.
    /// </summary>
    public class GenericWebAPIPatternDemoTests : IDisposable
    {
        private readonly EnhancedParameterBindingService _parameterBindingService;
        private readonly Mock<ILogger<EnhancedParameterBindingService>> _mockBindingLogger;

        public GenericWebAPIPatternDemoTests()
        {
            _mockBindingLogger = new Mock<ILogger<EnhancedParameterBindingService>>();
            _parameterBindingService = new EnhancedParameterBindingService(_mockBindingLogger.Object);
        }

        #region NestedResourceRoute Pattern Demonstrations

        [Fact]
        public async Task Demo_UserOrdersPattern_Success()
        {
            // Arrange - Simulate UserOrders endpoint: GET /api/organizations/{orgId}/users/{userId}/orders
            var methodInfo = typeof(MockUserOrdersController)
                .GetMethod(nameof(MockUserOrdersController.GetOrders))!;
            
            var schema = GenericWebAPITestPatterns.SchemaGenerationHelpers.CombineSchemas(
                new List<McpParameterInfo>
                {
                    GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("orgId", "route", true),
                    GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("userId", "route", true),
                    GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("pageSize", "query", false, 20),
                    GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("status", "query", false, "Active")
                }
            );

            // Act - Use the pattern helper method
            var boundParameters = await GenericWebAPITestPatterns.NestedResourceRoutePattern.TestSuccessfulBinding(
                _parameterBindingService, methodInfo, schema,
                orgId: 123, userId: 456,
                additionalParams: new Dictionary<string, object>
                {
                    { "pageSize", 50 },
                    { "status", "Completed" }
                });

            // Assert - Use pattern assertion helpers
            GenericWebAPITestPatterns.NestedResourceRoutePattern.AssertRouteParametersCorrect(boundParameters, 123, 456);
            Assert.Equal(4, boundParameters.Length);
            Assert.Equal(50, boundParameters[2]); // pageSize
            Assert.Equal("Completed", boundParameters[3]); // status
        }

        [Fact]
        public async Task Demo_UserProjectsPattern_WithDefaults()
        {
            // Arrange - Simulate UserProjects endpoint with optional pagination
            var methodInfo = typeof(MockUserProjectsController)
                .GetMethod(nameof(MockUserProjectsController.GetProjects))!;
            
            var schema = GenericWebAPITestPatterns.SchemaGenerationHelpers.CombineSchemas(
                new List<McpParameterInfo>
                {
                    GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("orgId", "route", true),
                    GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("userId", "route", true)
                },
                GenericWebAPITestPatterns.PaginationPattern.CreatePaginationSchema(defaultPageSize: 10)
            );

            // Act - Test with only required parameters (route params), should use defaults for optional
            var boundParameters = await GenericWebAPITestPatterns.NestedResourceRoutePattern.TestSuccessfulBinding(
                _parameterBindingService, methodInfo, schema,
                orgId: 789, userId: 101);

            // Assert - Route parameters correct, pagination defaults applied
            GenericWebAPITestPatterns.NestedResourceRoutePattern.AssertRouteParametersCorrect(boundParameters, 789, 101);
            Assert.Equal(6, boundParameters.Length); // orgId, userId, pageNumber, pageSize, sortBy, sortOrder
            Assert.Equal(1, boundParameters[2]); // pageNumber default
            Assert.Equal(10, boundParameters[3]); // pageSize default
            Assert.Equal("Id", boundParameters[4]); // sortBy default
            Assert.Equal("Ascending", boundParameters[5]); // sortOrder default
        }

        #endregion

        #region Complex Request Pattern Demonstrations

        [Fact]
        public async Task Demo_ProductCreationPattern_Success()
        {
            // Arrange - Simulate Product creation: POST /api/products
            var methodInfo = typeof(MockProductController)
                .GetMethod(nameof(MockProductController.CreateProduct))!;

            var requestProperties = new Dictionary<string, McpParameterInfo>
            {
                { "name", GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("name", "body", true) },
                { "category", GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("category", "body", true) },
                { "price", GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("price", "body", true) },
                { "isActive", GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateBooleanParameter("isActive", "body", false, true) }
            };

            var schema = GenericWebAPITestPatterns.ComplexRequestPattern.CreateSchemaWithComplexRequest(
                "productDto", 
                typeof(MockCreateProductDto), 
                requestProperties,
                new List<string> { "name", "category", "price" });

            var requestObject = new MockCreateProductDto
            {
                Name = "Premium Widget",
                Category = "Electronics",
                Price = 99.99m,
                IsActive = true
            };

            // Act - Use complex request pattern helper with correct parameter name
            var boundDto = await GenericWebAPITestPatterns.ComplexRequestPattern
                .TestComplexObjectBinding<MockCreateProductDto>(
                    _parameterBindingService, methodInfo, schema, requestObject, "productDto");

            // Assert - Complex object binding successful with all properties
            Assert.Equal("Premium Widget", boundDto.Name);
            Assert.Equal("Electronics", boundDto.Category);
            Assert.Equal(99.99m, boundDto.Price);
            Assert.True(boundDto.IsActive);
        }

        [Fact]
        public async Task Demo_OrderProcessingPattern_Success()
        {
            // Arrange - Simulate Order execution: POST /api/orders/execute
            var methodInfo = typeof(MockOrderController)
                .GetMethod(nameof(MockOrderController.ProcessOrder))!;

            var schema = GenericWebAPITestPatterns.BusinessWorkflowPattern.CreateWorkflowExecutionSchema();

            var orderRequest = new MockOrderProcessingRequest
            {
                OrgId = 123,
                UserId = 456,
                OrderType = "StandardOrder",
                Priority = "High"
            };

            // Act
            var boundDto = await GenericWebAPITestPatterns.ComplexRequestPattern
                .TestComplexObjectBinding<MockOrderProcessingRequest>(
                    _parameterBindingService, methodInfo, schema, orderRequest, "request");

            // Assert
            Assert.Equal(123, boundDto.OrgId);
            Assert.Equal(456, boundDto.UserId);
            Assert.Equal("StandardOrder", boundDto.OrderType);
            Assert.Equal("High", boundDto.Priority);
        }

        #endregion

        #region Enum Parameter Pattern Demonstrations

        [Fact]
        public async Task Demo_EnumParameterPattern_StringEnumSuccess()
        {
            // Arrange - Simulate endpoint with enum parameter
            var methodInfo = typeof(MockStatusController)
                .GetMethod(nameof(MockStatusController.UpdateStatus))!;

            var schema = new List<McpParameterInfo>
            {
                GenericWebAPITestPatterns.EnumParameterPattern.CreateEnumParameter<MockOrderStatus>(
                    "status", "query", true)
            };

            // Act & Assert - Test string enum binding
            await GenericWebAPITestPatterns.EnumParameterPattern.TestEnumBinding(
                _parameterBindingService, methodInfo, schema,
                "Processing", MockOrderStatus.Processing);
        }

        [Fact]
        public async Task Demo_EnumParameterPattern_NumericEnumSuccess()
        {
            // Arrange
            var methodInfo = typeof(MockStatusController)
                .GetMethod(nameof(MockStatusController.UpdateStatus))!;

            var schema = new List<McpParameterInfo>
            {
                GenericWebAPITestPatterns.EnumParameterPattern.CreateEnumParameter<MockOrderStatus>(
                    "status", "query", true)
            };

            // Update schema to accept numeric enum values
            schema[0].Type = "integer";

            var jsonParams = new Dictionary<string, object> { { "status", 2 } }; // Shipped = 2
            var jsonString = JsonSerializer.Serialize(jsonParams);
            var paramsJson = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var boundParameters = await _parameterBindingService.BindParametersAsync(
                methodInfo.GetParameters(), schema, paramsJson, true, methodInfo.Name);

            // Assert
            Assert.NotNull(boundParameters);
            Assert.Equal(MockOrderStatus.Shipped, boundParameters[0]);
        }

        #endregion

        #region Error Handling Pattern Demonstrations

        [Fact]
        public async Task Demo_ErrorHandling_MissingRequiredParameter()
        {
            // Arrange - Test missing orgId in nested resource route
            var methodInfo = typeof(MockUserOrdersController)
                .GetMethod(nameof(MockUserOrdersController.GetOrders))!;
            
            var schema = new List<McpParameterInfo>
            {
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("orgId", "route", true),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("userId", "route", true)
            };

            // Act & Assert - Use error handling pattern
            await GenericWebAPITestPatterns.ErrorHandlingPattern.TestMissingRequiredParameter(
                _parameterBindingService, methodInfo, schema, 
                "orgId", // Missing required parameter
                new Dictionary<string, object> { { "userId", 456 } }); // Only provide userId
        }

        [Fact]
        public async Task Demo_ErrorHandling_OptionalParameterDefaults()
        {
            // Arrange
            var methodInfo = typeof(MockSearchController)
                .GetMethod(nameof(MockSearchController.SearchProducts))!;
            
            var schema = new List<McpParameterInfo>
            {
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("query", "query", true),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("maxResults", "query", false, 50),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateBooleanParameter("includeInactive", "query", false, false)
            };

            // Act & Assert - Use error handling pattern to test defaults
            await GenericWebAPITestPatterns.ErrorHandlingPattern.TestOptionalParameterDefaults(
                _parameterBindingService, methodInfo, schema,
                requiredParams: new Dictionary<string, object> { { "query", "electronics" } },
                expectedDefaults: new Dictionary<string, object> 
                { 
                    { "maxResults", 50 }, 
                    { "includeInactive", false } 
                });
        }

        #endregion

        #region Performance Pattern Demonstrations

        [Fact]
        public async Task Demo_PerformancePattern_LargeParameterSet()
        {
            // Arrange - Simulate endpoint with many parameters (like analytics export endpoint)
            var methodInfo = typeof(MockReportController)
                .GetMethod(nameof(MockReportController.ExportData))!;

            var schema = new List<McpParameterInfo>
            {
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("orgId", "route", true),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("reportType", "query", true),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("dateFrom", "query", true),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("dateTo", "query", true),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("format", "query", false, "json"),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateBooleanParameter("includeMetadata", "query", false, false),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateIntegerParameter("batchSize", "query", false, 1000),
                GenericWebAPITestPatterns.SchemaGenerationHelpers.CreateStringParameter("compression", "query", false, "none")
            };

            var parameters = new Dictionary<string, object>
            {
                { "orgId", 123 },
                { "reportType", "OrderAnalytics" },
                { "dateFrom", "2024-01-01" },
                { "dateTo", "2024-12-31" },
                { "format", "csv" },
                { "includeMetadata", true },
                { "batchSize", 5000 },
                { "compression", "gzip" }
            };

            // Act & Assert - Use performance pattern (should complete in < 100ms)
            await GenericWebAPITestPatterns.PerformanceTestPattern.TestPerformanceWithinBounds(
                _parameterBindingService, methodInfo, schema, parameters, 
                expectedParameterCount: 8, maxMilliseconds: 100);
        }

        #endregion

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    #region Mock Controllers and Models for Pattern Demonstration

    public class MockUserOrdersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetOrders(int orgId, int userId, int pageSize, string status) => Ok();
    }

    public class MockUserProjectsController : ControllerBase
    {
        [HttpGet] 
        public IActionResult GetProjects(int orgId, int userId, int pageNumber, int pageSize, string sortBy, string sortOrder) => Ok();
    }

    public class MockProductController : ControllerBase
    {
        [HttpPost]
        public IActionResult CreateProduct(MockCreateProductDto productDto) => Ok();
    }

    public class MockOrderController : ControllerBase
    {
        [HttpPost]
        public IActionResult ProcessOrder(MockOrderProcessingRequest request) => Ok();
    }

    public class MockStatusController : ControllerBase
    {
        [HttpPut]
        public IActionResult UpdateStatus(MockOrderStatus status) => Ok();
    }

    public class MockSearchController : ControllerBase
    {
        [HttpGet]
        public IActionResult SearchProducts(string query, int maxResults = 50, bool includeInactive = false) => Ok();
    }

    public class MockReportController : ControllerBase
    {
        [HttpGet]
        public IActionResult ExportData(int orgId, string reportType, string dateFrom, string dateTo,
            string format = "json", bool includeMetadata = false, int batchSize = 1000, string compression = "none") => Ok();
    }

    public class MockCreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
    }

    public class MockOrderProcessingRequest
    {
        public int OrgId { get; set; }
        public int UserId { get; set; }
        public string OrderType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }

    public enum MockOrderStatus
    {
        Pending = 0,
        Processing = 1,
        Shipped = 2,
        Delivered = 3,
        Cancelled = 4
    }

    #endregion
}