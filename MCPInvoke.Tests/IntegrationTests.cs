using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MCPInvoke.AspNetCore.Extensions;
using MCPInvoke.Extensions;
using MCPInvoke.Services;
using MCPBuckle.Extensions;

namespace MCPInvoke.Tests
{
    public class IntegrationTests : IDisposable
    {
        private readonly TestServer _server;
        private readonly HttpClient _client;

        public IntegrationTests()
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddMcpBuckle(options =>
                    {
                        options.SchemaVersion = "1.0.0";
                        options.ServerTitle = "Test API";
                    });
                    
                    // Register MCPInvoke with MCPBuckle integration
                    services.AddMcpInvoke();
                    services.AddSingleton<IMcpToolDefinitionProvider, McpBuckleToolDefinitionProvider>();
                    services.AddSingleton<System.Reflection.Assembly>(System.Reflection.Assembly.GetExecutingAssembly());
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseMcpBuckle();
                    app.UseMcpInvoke();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapMcpInvoke("/mcp/invoke");
                    });
                });

            _server = new TestServer(builder);
            _client = _server.CreateClient();
        }

        public void Dispose()
        {
            _client?.Dispose();
            _server?.Dispose();
        }

        [Fact]
        public async Task FullMcpProtocol_InitializeFlow_WorksCorrectly()
        {
            // Step 1: Send initialize request
            var initializeRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-06-18",
                    clientInfo = new
                    {
                        name = "TestClient",
                        version = "1.0.0"
                    }
                },
                id = 1
            });

            var initResponse = await SendJsonRpcRequest(initializeRequest);
            var initJson = JsonDocument.Parse(initResponse);
            
            Assert.True(initJson.RootElement.TryGetProperty("result", out var initResult));
            Assert.Equal("2025-06-18", initResult.GetProperty("protocolVersion").GetString());
            Assert.True(initResult.TryGetProperty("capabilities", out var capabilities));
            Assert.True(capabilities.TryGetProperty("tools", out _));

            // Step 2: Send initialized notification
            var initializedNotification = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized",
                @params = new { },
                id = 2
            });

            var notifResponse = await SendJsonRpcRequest(initializedNotification);
            var notifJson = JsonDocument.Parse(notifResponse);
            
            Assert.True(notifJson.RootElement.TryGetProperty("result", out var notifResult));
            Assert.True(notifResult.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task ToolsList_ReturnsAvailableTools()
        {
            // Arrange
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/list",
                @params = new { },
                id = 3
            });

            // Act
            var response = await SendJsonRpcRequest(request);
            var responseJson = JsonDocument.Parse(response);

            // Assert
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var tools));
            
            var toolsArray = tools.EnumerateArray();
            Assert.NotEmpty(toolsArray);
            
            // Should contain tools from TestController
            bool foundGetData = false;
            bool foundCalculate = false;
            
            foreach (var tool in toolsArray)
            {
                var name = tool.GetProperty("name").GetString();
                if (name == "GetData") foundGetData = true;
                if (name == "Calculate") foundCalculate = true;
                
                Assert.True(tool.TryGetProperty("description", out _));
                Assert.True(tool.TryGetProperty("inputSchema", out _));
            }
            
            Assert.True(foundGetData);
            Assert.True(foundCalculate);
        }

        [Fact]
        public async Task ToolsCall_SimpleMethod_ExecutesSuccessfully()
        {
            // Arrange
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "GetData",
                    arguments = new { id = 42 }
                },
                id = 4
            });

            // Act
            var response = await SendJsonRpcRequest(request);
            var responseJson = JsonDocument.Parse(response);

            // Assert
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            // MCPInvoke returns the ASP.NET Core result object with a value property
            Assert.True(result.TryGetProperty("value", out var value));
            Assert.Contains("Data for ID: 42", value.GetString());
        }

        [Fact]
        public async Task ToolsCall_ComplexMethod_ExecutesSuccessfully()
        {
            // Arrange
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "Calculate",
                    arguments = new { a = 10, b = 5, operation = "multiply" }
                },
                id = 5
            });

            // Act
            var response = await SendJsonRpcRequest(request);
            var responseJson = JsonDocument.Parse(response);

            // Assert
            Assert.True(responseJson.RootElement.TryGetProperty("result", out var result));
            
            // MCPInvoke returns the ASP.NET Core result object with nested value
            Assert.True(result.TryGetProperty("value", out var value));
            Assert.True(value.TryGetProperty("result", out var calcResult));
            Assert.Equal(50, calcResult.GetDouble());
        }

        [Fact]
        public async Task ToolsCall_InvalidToolName_ReturnsError()
        {
            // Arrange
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "NonExistentTool",
                    arguments = new { }
                },
                id = 6
            });

            // Act
            var response = await SendJsonRpcRequest(request);
            var responseJson = JsonDocument.Parse(response);

            // Assert
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32601, error.GetProperty("code").GetInt32()); // Method not found
            Assert.Contains("not found", error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ToolsCall_MissingRequiredParameter_ReturnsError()
        {
            // Arrange
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "Calculate",
                    arguments = new { a = 10 } // Missing 'b' parameter
                },
                id = 7
            });

            // Act
            var response = await SendJsonRpcRequest(request);
            var responseJson = JsonDocument.Parse(response);

            // Assert
            Assert.True(responseJson.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32602, error.GetProperty("code").GetInt32()); // Invalid params
        }

        [Fact]
        public async Task McpContext_Endpoint_ReturnsValidJson()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonDocument.Parse(json);
            
            Assert.True(context.RootElement.TryGetProperty("tools", out var tools));
            Assert.NotEmpty(tools.EnumerateArray());
        }

        private async Task<string> SendJsonRpcRequest(string jsonRequest)
        {
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/mcp/invoke", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    // Test controller for integration tests
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        /// <summary>
        /// Gets data by ID
        /// </summary>
        /// <param name="id">The data ID</param>
        /// <returns>Data string</returns>
        [HttpGet("data/{id}")]
        public IActionResult GetData(int id)
        {
            return Ok($"Data for ID: {id}");
        }

        /// <summary>
        /// Performs a calculation
        /// </summary>
        /// <param name="a">First number</param>
        /// <param name="b">Second number</param>
        /// <param name="operation">Operation to perform (add, subtract, multiply, divide)</param>
        /// <returns>Calculation result</returns>
        [HttpPost("calculate")]
        public IActionResult Calculate(double a, double b, string operation = "add")
        {
            double result = operation.ToLower() switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : double.NaN,
                _ => double.NaN
            };

            return Ok(new { result, operation });
        }
    }
}