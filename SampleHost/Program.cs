using MCPInvoke.Services;
using MCPInvoke.Extensions; // Corrected from MCPInvoke.Hosting
using MCPInvoke; // For McpToolDefinition, McpParameterInfo
using System.Reflection;
using MCPBuckle.Extensions; // Added for AddMcpBuckle()

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Register MCPInvoke services
builder.Services.AddMcpInvoke();

// --- MCP Tool Definition Provider Setup ---
// Get the assembly containing the tools to be scanned. 
// For SampleHost, this is the SampleHost assembly itself, assuming SampleToolService is in it.
// If SampleToolService were in a different assembly, specify that assembly here.
Assembly assemblyToScan = typeof(Program).Assembly; 
builder.Services.AddSingleton(assemblyToScan); // Make the assembly available for injection

// Add MCPBuckle services (this should register IControllerDiscoveryService)
builder.Services.AddMcpBuckle();

// Register the McpBuckleToolDefinitionProvider as the IMcpToolDefinitionProvider
// It now depends on IControllerDiscoveryService from MCPBuckle and the Assembly to scan.
builder.Services.AddSingleton<IMcpToolDefinitionProvider, McpBuckleToolDefinitionProvider>();

// --- End MCP Tool Definition Provider Setup ---

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.UseSwagger(); // If you have Swagger/OpenAPI docs
    // app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Optional: if you want to enforce HTTPS

app.UseAuthorization();

app.MapControllers();

// Map the MCP Invoke endpoint
app.MapMcpInvoke("/mcpinvoke");

// --- Test code to print registered MCP tool definitions ---
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var mcpToolProvider = serviceProvider.GetRequiredService<IMcpToolDefinitionProvider>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Retrieving MCP Tool Definitions for testing...");
        var toolDefinitions = mcpToolProvider.GetToolDefinitions();
        if (toolDefinitions != null && toolDefinitions.Any())
        {
            logger.LogInformation("Found {Count} MCP Tool Definitions:", toolDefinitions.Count());
            foreach (var toolDef in toolDefinitions)
            {
                logger.LogInformation("  Tool: {Name}, Description: {Description}, Handler: {Handler}, Method: {Method}", 
                                    toolDef.Name, 
                                    toolDef.Description, 
                                    toolDef.HandlerTypeAssemblyQualifiedName, 
                                    toolDef.MethodName);
                if (toolDef.InputSchema != null && toolDef.InputSchema.Any())
                {
                    logger.LogInformation("    InputSchema:");
                    foreach (var param in toolDef.InputSchema)
                    {
                        logger.LogInformation("      Param: {ParamName}, Type: {ParamType}, Required: {IsRequired}, Desc: {ParamDesc}", 
                                            param.Name, param.Type, param.IsRequired, param.Description);
                    }
                }
                else
                {
                    logger.LogInformation("    InputSchema: None");
                }
            }
        }
        else
        {
            logger.LogWarning("No MCP Tool Definitions found.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving or printing MCP Tool Definitions.");
    }
}
// --- End test code ---


app.Run();
