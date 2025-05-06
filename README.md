# MCPInvoke

MCPInvoke is a custom MCP execution endpoint, designed as a companion to the MCPBuckle library (which handles MCP discovery). MCPInvoke manages the execution of tools defined by MCP requests.

## Purpose

This library enables ASP.NET Core applications with existing REST APIs and Swagger/OpenAPI documentation to easily become MCP-enabled. It provides a standardized execution endpoint for tools discovered via MCPBuckle.

## Features

- **Attribute-Driven (Optional, Extensible)** - Leverages existing OpenAPI/Swagger metadata by default, with optional MCP-specific attributes for customization
- **Standard-Compliant** - Implements JSON-RPC 2.0 and MCP specifications
- **Developer-Friendly** - Minimal friction for projects already using Swagger/OpenAPI
- **Secure** - Input validation, sanitized outputs, and support for human-in-the-loop confirmations

## Getting Started

### Installation

```bash
dotnet add package MCPInvoke
```

### Basic Setup

```csharp
// In Program.cs or Startup.cs
using MCPInvoke.Extensions;

// Register MCPInvoke services
builder.Services.AddMcpInvoke();

// Register an implementation of IMcpToolDefinitionProvider
// See below for ASP.NET Core controller integration

// Register endpoint
app.MapMcpInvoke("/mcpinvoke");
```

### ASP.NET Core Controller Integration

Version 1.0.2 adds automatic ASP.NET Core controller integration:

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

// Map the MCP Invoke endpoint
app.MapMcpInvoke("/mcpinvoke");
```

For more detailed information on ASP.NET Core integration, see the [AspNetCore README](MCPInvoke/AspNetCore/README.md).
