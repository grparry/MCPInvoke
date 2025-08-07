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

namespace MCPInvoke.Debug
{
    public class DebugSchemaGeneration
    {
        public static void Main(string[] args)
        {
            var loggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var options = new AspNetControllerToolOptions();
            
            var provider = new AspNetControllerToolDefinitionProvider(
                serviceProviderMock.Object, 
                loggerMock.Object, 
                options, 
                new[] { typeof(DebugController).Assembly });
            
            serviceProviderMock.Setup(sp => sp.GetService(typeof(DebugController)))
                .Returns(new DebugController());
            
            var tools = provider.GetToolDefinitions();
            var tool = tools.FirstOrDefault(t => t.Name == "Debug_ProcessBatchRequest");
            
            Console.WriteLine($"Tool found: {tool != null}");
            if (tool != null)
            {
                Console.WriteLine($"Tool name: {tool.Name}");
                Console.WriteLine($"Parameters count: {tool.InputSchema.Count}");
                
                var requestParam = tool.InputSchema.FirstOrDefault(p => p.Name == "request");
                if (requestParam != null)
                {
                    Console.WriteLine($"Request param type: {requestParam.Type}");
                    Console.WriteLine($"Has annotations: {requestParam.Annotations != null}");
                    
                    if (requestParam.Annotations != null && requestParam.Annotations.ContainsKey("properties"))
                    {
                        var properties = requestParam.Annotations["properties"] as Dictionary<string, object>;
                        Console.WriteLine($"Properties found: {properties?.Count ?? 0}");
                        if (properties != null)
                        {
                            foreach (var prop in properties)
                            {
                                Console.WriteLine($"Property: {prop.Key}");
                                if (prop.Value is Dictionary<string, object> propDef)
                                {
                                    Console.WriteLine($"  Type: {propDef.GetValueOrDefault("type", "unknown")}");
                                }
                            }
                            
                            if (properties.ContainsKey("Items"))
                            {
                                var itemsProperty = properties["Items"] as Dictionary<string, object>;
                                Console.WriteLine($"Items property type: {itemsProperty?.GetValueOrDefault("type", "unknown")}");
                            }
                        }
                    }
                }
            }
        }
    }
    
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        [HttpPost("process-batch")]
        public IActionResult ProcessBatchRequest([FromBody] DebugBatchRequest request)
        {
            return Ok("Processed batch");
        }
    }
    
    public class DebugBatchRequest
    {
        public string BatchName { get; set; } = string.Empty;
        public List<DebugItem> Items { get; set; } = new();
    }
    
    public class DebugItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}