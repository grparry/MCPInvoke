using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MCPInvoke.AspNetCore;
using MCPInvoke;
using MCPInvoke.Services;

namespace MCPInvoke.Debug
{
    public class DebugErrorCodeGeneration
    {
        public static void Main(string[] args)
        {
            var loggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var executionLoggerMock = new Mock<ILogger<McpExecutionService>>();
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            var options = new AspNetControllerToolOptions();
            
            var provider = new AspNetControllerToolDefinitionProvider(
                serviceProviderMock.Object, 
                loggerMock.Object, 
                options, 
                new[] { typeof(DebugCalculatorController).Assembly });
            
            serviceProviderMock.Setup(sp => sp.GetService(typeof(DebugCalculatorController)))
                .Returns(new DebugCalculatorController());
                
            var serviceScopeMock = new Mock<IServiceScope>();
            serviceScopeMock.Setup(scope => scope.ServiceProvider)
                .Returns(serviceProviderMock.Object);
            serviceScopeFactoryMock.Setup(factory => factory.CreateScope())
                .Returns(serviceScopeMock.Object);
            
            var executionService = new McpExecutionService(serviceScopeFactoryMock.Object, provider, executionLoggerMock.Object);
            
            var tools = provider.GetToolDefinitions();
            var calculateTool = tools.FirstOrDefault(t => t.Name == "DebugCalculator_Calculate");
            
            Console.WriteLine($"Tool found: {calculateTool != null}");
            if (calculateTool != null)
            {
                Console.WriteLine($"Tool name: {calculateTool.Name}");
                Console.WriteLine($"Parameters count: {calculateTool.InputSchema.Count}");
                
                foreach (var param in calculateTool.InputSchema)
                {
                    Console.WriteLine($"Parameter: {param.Name}, Type: {param.Type}, Required: {param.IsRequired}");
                }
                
                // Test the exact scenario from the failing test
                var mcpRequest = JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    method = "tools/call",
                    @params = new
                    {
                        name = "DebugCalculator_Calculate",
                        arguments = new { a = 10.0 } // Missing 'b' parameter
                    },
                    id = 7
                });
                
                Console.WriteLine($"Request: {mcpRequest}");
                
                try
                {
                    var response = executionService.ProcessRequestAsync(mcpRequest).Result;
                    Console.WriteLine($"Response: {response}");
                    
                    var responseJson = JsonDocument.Parse(response);
                    if (responseJson.RootElement.TryGetProperty("error", out var error))
                    {
                        var code = error.GetProperty("code").GetInt32();
                        var message = error.GetProperty("message").GetString();
                        Console.WriteLine($"Error code: {code} (expecting -32602)");
                        Console.WriteLine($"Error message: {message}");
                    }
                    else
                    {
                        Console.WriteLine("No error property found in response");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception during execution: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }
    }
    
    [ApiController]
    [Route("api/debug-calculator")]
    public class DebugCalculatorController : ControllerBase
    {
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
            
            return Ok(new { result });
        }
    }
}