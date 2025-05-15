# MCPInvoke

MCPInvoke is a custom MCP execution endpoint, designed as a companion to the MCPBuckle library (which handles MCP discovery). MCPInvoke manages the execution of tools defined by MCP requests.

## Purpose

This library enables ASP.NET Core applications with existing REST APIs and Swagger/OpenAPI documentation to easily become MCP-enabled. It provides a standardized execution endpoint for tools discovered via MCPBuckle.

## Features

- **Attribute-Driven (Optional, Extensible)** - Leverages existing OpenAPI/Swagger metadata by default, with optional MCP-specific attributes for customization
- **Standard-Compliant** - Implements JSON-RPC 2.0 and MCP specifications
- **Developer-Friendly** - Minimal friction for projects already using Swagger/OpenAPI
- **Secure** - Input validation, sanitized outputs, and support for human-in-the-loop confirmations

## What's New in 1.2.0

- **Enhanced API Response Handling** - Improved handling of complex ASP.NET Core return types including ActionResult<T>, ObjectResult, and other IActionResult implementations
- **MCPBuckle 1.5.0 Integration** - Updated to use MCPBuckle 1.5.0 with enhanced schema generation, better parameter binding, and improved enum support
- **Advanced Enum Support** - Added full support for enum types, including both integer and string-based enums with JsonStringEnumConverter compatibility
- **Collection Type Support** - Better handling of array and collection return types for improved client compatibility
- **New Middleware Architecture** - Introduced dedicated McpInvokeMiddleware for more efficient request processing
- **Simplified Configuration** - Added ApplicationBuilderExtensions with UseMcpInvoke extension method for easier integration
- **Parameter Annotations** - Added support for additional parameter metadata through Annotations property

## What's New in 1.1.0

- **MCPBuckle 1.4.0 Integration** - Updated to use the MCPBuckle 1.4.0 NuGet package for better MCP specification compliance
- **Special JSON-RPC Method Handling** - Added support for standard JSON-RPC methods:
  - `notifications/initialized` - Properly acknowledges initialization notifications from MCP clients
  - `tools/list` - Returns a helpful error message directing clients to use the MCP context endpoint instead

## Getting Started

### Installation

```bash
dotnet add package MCPInvoke
```

### Basic Setup

```csharp
// In Program.cs or Startup.cs
using MCPInvoke.Extensions;
using MCPInvoke.AspNetCore.Extensions;

// Register MCPInvoke services
builder.Services.AddMcpInvoke();

// Register an implementation of IMcpToolDefinitionProvider
// See below for ASP.NET Core controller integration

// Register endpoint using the middleware
app.UseMcpInvoke("/mcpinvoke");
```

### ASP.NET Core Controller Integration

MCPInvoke provides automatic ASP.NET Core controller integration:

```csharp
// In Program.cs or Startup.cs
using MCPInvoke.AspNetCore.Extensions;

// Register MCPInvoke with ASP.NET Core controller integration
builder.Services.AddMcpInvokeWithControllers(options => {
    // Include controller name in tool name
    options.IncludeControllerNameInToolName = true;
    
    // Optionally exclude specific controllers
    // options.ExcludedControllers.Add("Health");
});

// Use the MCP Invoke middleware
app.UseMcpInvoke("/mcpinvoke");
```

For more detailed information on ASP.NET Core integration, see the [AspNetCore README](MCPInvoke/AspNetCore/README.md).
