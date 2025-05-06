using MCPInvoke;
using MCPInvoke.Testing;
using System.Collections.Generic;

namespace SampleHost;

public class MockMcpToolDefinitionProvider : IMcpToolDefinitionProvider
{
    public IEnumerable<McpToolDefinition> GetToolDefinitions()
    {
        var sampleToolServiceTypeAssemblyQualifiedName = typeof(SampleToolService).AssemblyQualifiedName!;

        return new List<McpToolDefinition>
        {
            new McpToolDefinition
            {
                Name = "Sample.Add",
                HandlerTypeAssemblyQualifiedName = sampleToolServiceTypeAssemblyQualifiedName,
                MethodName = nameof(SampleToolService.Add),
                InputSchema = new List<McpParameterInfo>
                {
                    new McpParameterInfo { Name = "a", Type = "integer", IsRequired = true },
                    new McpParameterInfo { Name = "b", Type = "integer", IsRequired = true }
                }
            },
            new McpToolDefinition
            {
                Name = "Sample.Greet",
                HandlerTypeAssemblyQualifiedName = sampleToolServiceTypeAssemblyQualifiedName,
                MethodName = nameof(SampleToolService.Greet),
                InputSchema = new List<McpParameterInfo>
                {
                    new McpParameterInfo { Name = "name", Type = "string", IsRequired = true },
                    new McpParameterInfo { Name = "title", Type = "string", IsRequired = false }
                }
            },
            new McpToolDefinition
            {
                Name = "Sample.GetServerTimeAsync",
                HandlerTypeAssemblyQualifiedName = sampleToolServiceTypeAssemblyQualifiedName,
                MethodName = nameof(SampleToolService.GetServerTimeAsync),
                InputSchema = new List<McpParameterInfo>
                {
                    new McpParameterInfo { Name = "includeMilliseconds", Type = "boolean", IsRequired = false }
                }
            },
            new McpToolDefinition
            {
                Name = "Sample.GetVersion", // Static method
                HandlerTypeAssemblyQualifiedName = sampleToolServiceTypeAssemblyQualifiedName,
                MethodName = nameof(SampleToolService.GetVersion),
                InputSchema = new List<McpParameterInfo>() // No parameters
            },
            new McpToolDefinition
            {
                Name = "Sample.TestError",
                HandlerTypeAssemblyQualifiedName = sampleToolServiceTypeAssemblyQualifiedName,
                MethodName = nameof(SampleToolService.TestError),
                InputSchema = new List<McpParameterInfo>
                {
                    new McpParameterInfo { Name = "message", Type = "string", IsRequired = true }
                }
            }
        };
    }
}
