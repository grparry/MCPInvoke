using MCPInvoke.Models; // For McpToolDefinition, McpToolSchemaProperty - RETAIN FOR NOW, MIGHT BE USED BY OTHER PARTS
using MCPInvoke; // Added for McpParameterInfo and McpToolDefinition from root namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging; // Added missing using directive
using MCPBuckle.Services; // Added for IControllerDiscoveryService
using MCPBuckle.Models;   // Added for McpTool, McpSchema

namespace MCPInvoke.Services
{
    /// <summary>
    /// Provides MCP tool definitions by leveraging an MCPBuckle tool scanner.
    /// This implementation adapts tool information discovered by MCPBuckle
    /// into the McpToolDefinition format expected by MCPInvoke.
    /// </summary>
    public class McpBuckleToolDefinitionProvider : IMcpToolDefinitionProvider
    {
        private readonly Assembly _assemblyToScan; // May not be needed if IControllerDiscoveryService handles assembly context
        private readonly IControllerDiscoveryService _toolScanner; 
        private readonly ILogger<McpBuckleToolDefinitionProvider> _logger;
        private List<McpToolDefinition>? _toolDefinitions; // Cached list
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="McpBuckleToolDefinitionProvider"/> class.
        /// </summary>
        /// <param name="toolScanner">The MCPBuckle controller discovery service.</param>
        /// <param name="assemblyToScan">The assembly to scan for tool definitions. This might be used by specific scanner implementations or for context, though IControllerDiscoveryService might manage its own assembly context.</param>
        /// <param name="logger">The logger for this provider.</param>
        public McpBuckleToolDefinitionProvider(
            IControllerDiscoveryService toolScanner, 
            Assembly assemblyToScan, // Keep for now, might be used by specific scanner implementations or for context
            ILogger<McpBuckleToolDefinitionProvider> logger)
        {
            _toolScanner = toolScanner ?? throw new ArgumentNullException(nameof(toolScanner));
            _assemblyToScan = assemblyToScan ?? throw new ArgumentNullException(nameof(assemblyToScan));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("McpBuckleToolDefinitionProvider initialized with IControllerDiscoveryService. Assembly to scan: {AssemblyName}", _assemblyToScan.FullName);
        }

        /// <summary>
        /// Retrieves the collection of MCP tool definitions, loading them if not already cached.
        /// </summary>
        /// <returns>An enumerable collection of <see cref="McpToolDefinition"/>.</returns>
        public IEnumerable<McpToolDefinition> GetToolDefinitions()
        {
            if (_toolDefinitions == null)
            {
                lock (_lock)
                {
                    if (_toolDefinitions == null) 
                    {
                        _logger.LogInformation("Tool definitions cache miss. Loading definitions from MCPBuckle IControllerDiscoveryService.");
                        _toolDefinitions = LoadToolDefinitionsFromBuckle();
                    }
                }
            }
            else
            {
                _logger.LogDebug("Returning cached tool definitions.");
            }
            return _toolDefinitions;
        }

        private List<McpToolDefinition> LoadToolDefinitionsFromBuckle()
        {
            var mcpInvokeDefinitions = new List<McpToolDefinition>();
            _logger.LogInformation(
                "Discovering tools using IControllerDiscoveryService for assembly context: {AssemblyName}.", 
                _assemblyToScan.FullName); // Assembly context might be implicitly handled by the scanner

            List<McpTool> discoveredTools;
            try
            {
                // Note: IControllerDiscoveryService.DiscoverTools() might not need _assemblyToScan directly
                // if it's configured globally or operates on the entry assembly by default.
                // For now, assuming it implicitly uses its configured context.
                discoveredTools = _toolScanner.DiscoverTools();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while discovering tools with IControllerDiscoveryService.");
                return mcpInvokeDefinitions; // Return empty list on error
            }

            if (discoveredTools == null || !discoveredTools.Any())
            {
                _logger.LogWarning("IControllerDiscoveryService returned no tools.");
                return mcpInvokeDefinitions;
            }

            _logger.LogInformation(
                "Mapping {ToolCount} discovered MCPBuckle.Models.McpTool objects to McpToolDefinition format.", 
                discoveredTools.Count);

            foreach (var buckleTool in discoveredTools)
            {
                if (buckleTool == null || string.IsNullOrEmpty(buckleTool.Name))
                {
                    _logger.LogWarning("Skipping a discovered MCPBuckle tool due to missing Name.");
                    continue;
                }

                try
                {
                    var inputParams = new List<McpParameterInfo>();
                    if (buckleTool.InputSchema?.Properties != null)
                    {
                        foreach (var prop in buckleTool.InputSchema.Properties)
                        {
                            var paramName = prop.Key;
                            var paramSchema = prop.Value;
                            if (paramSchema == null) continue;

                            inputParams.Add(new McpParameterInfo
                            {
                                Name = paramName,
                                Type = paramSchema.Type, // Or paramSchema.Format if more specific
                                Description = paramSchema.Description,
                                IsRequired = buckleTool.InputSchema.Required?.Contains(paramName) ?? false
                            });
                        }
                    }
                    
                    // Log all annotations for debugging
                    var annotationsJson = JsonSerializer.Serialize(buckleTool.Annotations);
                    _logger.LogInformation("Annotations for tool '{ToolName}': {Annotations}", buckleTool.Name, annotationsJson);

                    // Extract HandlerTypeAssemblyQualifiedName and MethodName from annotations
                    string handlerTypeAssemblyQualifiedName = buckleTool.Annotations.TryGetValue("HandlerTypeAssemblyQualifiedName", out var handlerObj) && handlerObj is string handlerStr ? handlerStr : "UNKNOWN_HANDLER_TYPE";
                    string methodName = buckleTool.Annotations.TryGetValue("MethodName", out var methodObj) && methodObj is string methodStr ? methodStr : "UNKNOWN_METHOD_NAME";

                    mcpInvokeDefinitions.Add(new McpToolDefinition
                    {
                        Name = buckleTool.Name, 
                        Description = buckleTool.Description,
                        HandlerTypeAssemblyQualifiedName = handlerTypeAssemblyQualifiedName, 
                        MethodName = methodName,
                        InputSchema = inputParams
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error mapping MCPBuckle.Models.McpTool '{ToolName}' to McpToolDefinition.", buckleTool.Name);
                }
            }
            
            _logger.LogInformation(
                "Successfully mapped {MappedCount} MCPToolDefinitions.", 
                mcpInvokeDefinitions.Count);

            return mcpInvokeDefinitions;
        }
    }
}
