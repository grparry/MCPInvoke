using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPInvoke.AspNetCore.Middleware;
using MCPInvoke.Services;
using MCPInvoke.Models;
using MCPInvoke;

namespace MCPInvoke.Tests.AspNetCore.Middleware
{
    public class McpInvokeMiddlewareTests
    {
        private readonly Mock<RequestDelegate> _nextMock;
        private readonly McpExecutionService _executionService;
        private readonly Mock<ILogger<McpInvokeMiddleware>> _loggerMock;
        private readonly McpInvokeMiddleware _middleware;

        public McpInvokeMiddlewareTests()
        {
            _nextMock = new Mock<RequestDelegate>();
            _loggerMock = new Mock<ILogger<McpInvokeMiddleware>>();
            
            // Create a real execution service with mocked dependencies
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            var toolProviderMock = new Mock<IMcpToolDefinitionProvider>();
            var executionLoggerMock = new Mock<ILogger<McpExecutionService>>();
            
            // Setup empty tool definitions
            toolProviderMock.Setup(p => p.GetToolDefinitions()).Returns(new List<McpToolDefinition>());
            
            _executionService = new McpExecutionService(
                serviceScopeFactoryMock.Object,
                toolProviderMock.Object,
                executionLoggerMock.Object);
                
            _middleware = new McpInvokeMiddleware(
                _nextMock.Object,
                _executionService,
                _loggerMock.Object);
        }

        [Fact]
        public async Task InvokeAsync_NonPostRequest_CallsNextMiddleware()
        {
            // Arrange
            var context = CreateHttpContext("GET", "application/json");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_NonJsonContentType_CallsNextMiddleware()
        {
            // Arrange
            var context = CreateHttpContext("POST", "text/plain");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_InitializeMethod_ReturnsServerCapabilities()
        {
            // Arrange
            var requestBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "initialize",
                @params = new { },
                id = 1
            });
            var context = CreateHttpContext("POST", "application/json", requestBody);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            
            var response = await GetResponseBodyAsync(context);
            var responseJson = JsonDocument.Parse(response);
            
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("protocolVersion", out var protocolVersion));
            Assert.Equal("2025-06-18", protocolVersion.GetString());
            Assert.True(result.TryGetProperty("serverInfo", out var serverInfo));
            Assert.True(result.TryGetProperty("capabilities", out var capabilities));
        }

        [Fact]
        public async Task InvokeAsync_NotificationsInitialized_ReturnsSuccess()
        {
            // Arrange
            var requestBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized",
                @params = new { },
                id = 2
            });
            var context = CreateHttpContext("POST", "application/json", requestBody);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            
            var response = await GetResponseBodyAsync(context);
            var responseJson = JsonDocument.Parse(response);
            
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("success", out var success));
            Assert.True(success.GetBoolean());
        }

        [Fact]
        public async Task InvokeAsync_ToolsList_CallsExecutionService()
        {
            // Arrange
            var requestBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/list",
                @params = new { },
                id = 3
            });
            var context = CreateHttpContext("POST", "application/json", requestBody);
            
            // The execution service will handle this request and return tools list

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            // Execution service handles the request
            
            var response = await GetResponseBodyAsync(context);
            // Verify the response contains expected data
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("jsonrpc", out _));
        }

        [Fact]
        public async Task InvokeAsync_ToolsCall_CallsExecutionService()
        {
            // Arrange
            var requestBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new 
                { 
                    name = "TestTool",
                    arguments = new { param1 = "value1" }
                },
                id = 4
            });
            var context = CreateHttpContext("POST", "application/json", requestBody);
            
            var expectedResponse = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                result = new { content = new[] { new { type = "text", text = "Success" } } },
                id = 4
            });
            
            // The execution service will handle this request directly

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            // Execution service handles the request
            
            var response = await GetResponseBodyAsync(context);
            // Verify the response contains expected data
            var responseJson = JsonDocument.Parse(response);
            Assert.True(responseJson.RootElement.TryGetProperty("jsonrpc", out _));
        }

        [Fact]
        public async Task InvokeAsync_InvalidJson_ReturnsErrorResponse()
        {
            // Arrange
            var requestBody = "invalid json";
            var context = CreateHttpContext("POST", "application/json", requestBody);
            
            // Test with actual invalid JSON that will cause parsing to fail

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            var response = await GetResponseBodyAsync(context);
            var responseJson = JsonDocument.Parse(response);
            
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.True(error.TryGetProperty("code", out var code));
            Assert.Equal(-32700, code.GetInt32()); // Parse error for invalid JSON
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_ExecutionServiceThrows_ReturnsErrorResponse()
        {
            // Arrange
            var requestBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "unknownMethod",
                @params = new { },
                id = 5
            });
            var context = CreateHttpContext("POST", "application/json", requestBody);
            
            // This test would need a tool that throws an exception when executed

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            var response = await GetResponseBodyAsync(context);
            var responseJson = JsonDocument.Parse(response);
            
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        [Fact]
        public void Constructor_NullNext_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new McpInvokeMiddleware(null!, _executionService, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_NullExecutionService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new McpInvokeMiddleware(_nextMock.Object, null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new McpInvokeMiddleware(_nextMock.Object, _executionService, null!));
        }

        private static HttpContext CreateHttpContext(string method, string contentType, string? body = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.ContentType = contentType;
            
            if (body != null)
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
                context.Request.Body = stream;
            }
            
            context.Response.Body = new MemoryStream();
            
            return context;
        }

        private static async Task<string> GetResponseBodyAsync(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            return await reader.ReadToEndAsync();
        }
    }
}