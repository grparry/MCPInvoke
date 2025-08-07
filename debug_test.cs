using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MCPInvoke.AspNetCore;

// Simple debug program to see what tools are generated
var loggerMock = new Mock<ILogger<AspNetControllerToolDefinitionProvider>>();
var serviceProviderMock = new Mock<IServiceProvider>();

// Setup controller
serviceProviderMock.Setup(sp => sp.GetService(typeof(SimpleRouteController)))
    .Returns(new SimpleRouteController());

var options = new AspNetControllerToolOptions 
{ 
    IncludeControllerNameInToolName = true 
};
var provider = new AspNetControllerToolDefinitionProvider(serviceProviderMock.Object, loggerMock.Object, options);

Console.WriteLine("Generated tools:");
var tools = provider.GetToolDefinitions().ToList();
foreach (var tool in tools)
{
    Console.WriteLine($"  - Name: '{tool.Name}', Method: '{tool.MethodName}', Handler: '{tool.HandlerTypeAssemblyQualifiedName}'");
    foreach (var param in tool.InputSchema)
    {
        Console.WriteLine($"    - Param: '{param.Name}' ({param.Type})");
    }
}

Console.WriteLine($"Total tools: {tools.Count}");

[ApiController]
[Route("api/simple")]
public class SimpleRouteController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        return Ok($"Item {id}");
    }
}