# MCPInvoke ASP.NET Core Controller Integration

This module provides seamless integration between ASP.NET Core controllers and the Model Context Protocol (MCP) by automatically exposing your API controllers as MCP tools.

## Overview

The ASP.NET Core Controller integration simplifies the process of exposing your Web API endpoints as MCP tools that can be discovered and executed by AI agents. Instead of having to manually define MCP tools, this integration automatically scans your controller classes and exposes their action methods as MCP tools.

## Features

- **Automatic Controller Discovery**: Scans your assembly for ASP.NET Core controllers
- **Configurable Tool Naming**: Customize the naming scheme for MCP tools
- **Controller Filtering**: Exclude specific controllers from being exposed as MCP tools
- **Infrastructure Parameter Handling**: Automatically filters out ASP.NET Core infrastructure parameters that aren't relevant to MCP
- **Type Mapping**: Maps .NET types to JSON Schema types for proper MCP tool definition

## Installation

This feature is included in MCPInvoke version 1.0.1 and later.

```bash
dotnet add package MCPInvoke
```

## Usage

### Basic Setup

```csharp
// In Program.cs or Startup.cs
using MCPInvoke.AspNetCore.Extensions;

// Register MCPInvoke with ASP.NET Core controller integration
builder.Services.AddMcpInvokeWithControllers();

// ...

// Map the MCP Invoke endpoint
app.MapMcpInvoke("/mcpinvoke");
```

### Customizing Controller Integration

```csharp
// Register with custom options
builder.Services.AddMcpInvokeWithControllers(options => {
    // Include controller name in tool name (e.g., "Customers_GetCustomer" instead of just "GetCustomer")
    options.IncludeControllerNameInToolName = true;
    
    // Exclude specific controllers from being exposed as MCP tools
    options.ExcludedControllers.Add("Health");
    options.ExcludedControllers.Add("Metrics");
});
```

### Scanning Custom Assemblies

By default, the integration scans the entry assembly for controllers. If your controllers are in different assemblies, you can specify them:

```csharp
// Scan multiple assemblies for controllers
var assemblies = new[] { 
    typeof(Program).Assembly,
    typeof(ExternalController).Assembly 
};

builder.Services.AddMcpInvokeWithControllers(
    options => { /* Configure options */ },
    assemblies);
```

## How It Works

The integration uses reflection to:

1. Find all classes deriving from `ControllerBase`
2. Find action methods in those classes (methods with HTTP verb attributes)
3. Convert the controller actions to MCP tool definitions with appropriate names and parameter schemas
4. Register these with MCPInvoke's tool execution system

## Supported Types

The integration maps .NET types to appropriate JSON Schema types:

- **Numeric types** (int, long, float, double, decimal) → "integer" or "number"
- **String** → "string"
- **Boolean** → "boolean"
- **DateTime, DateTimeOffset, TimeSpan** → "string" (with appropriate format)
- **Enums** → "string"
- **Arrays and collections** → "array"
- **Complex types** → "object"

## Example

If you have a controller like this:

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<Customer>> GetCustomer(int id)
    {
        // Implementation
    }
    
    [HttpPost]
    public async Task<ActionResult<Customer>> CreateCustomer(CustomerDto customer)
    {
        // Implementation
    }
}
```

The integration will expose MCP tools named:
- `Customers_GetCustomer` with an "id" parameter of type "integer"
- `Customers_CreateCustomer` with complex parameter mapping for the CustomerDto

## Limitations

- Currently, complex parameter types are exposed as "object" type. Future enhancements will better represent complex type properties.
- Form file uploads are not currently supported in the MCP context.
- Route constraints and binding sources are not reflected in the MCP tool definition.
