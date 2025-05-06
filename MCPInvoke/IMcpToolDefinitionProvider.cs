using System.Collections.Generic;
// This namespace might change if we later move it to a subfolder
// For now, keeping it aligned with the project's root namespace for simplicity.
namespace MCPInvoke;

/// <summary>
/// Placeholder interface for a service that provides MCP tool definitions.
/// This would typically be implemented or provided by MCPBuckle.
/// </summary>
public interface IMcpToolDefinitionProvider
{
    /// <summary>
    /// Gets the collection of MCP tool definitions.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="McpToolDefinition"/>.</returns>
    IEnumerable<McpToolDefinition> GetToolDefinitions();
}
