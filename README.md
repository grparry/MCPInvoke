# MCPInvoke

MCPInvoke is a complete Model Context Protocol (MCP) server implementation for ASP.NET Core applications. Originally designed as a companion to MCPBuckle for tool execution, MCPInvoke now provides full MCP protocol support including tool discovery and execution.

## Purpose

This library enables ASP.NET Core applications with existing REST APIs and Swagger/OpenAPI documentation to easily become MCP-enabled. It provides a complete MCP server implementation that handles both tool discovery and execution, making your APIs accessible to AI agents and tools like Claude Code CLI.

## Features

- **Attribute-Driven (Optional, Extensible)** - Leverages existing OpenAPI/Swagger metadata by default, with optional MCP-specific attributes for customization
- **Standard-Compliant** - Implements JSON-RPC 2.0 and MCP specifications
- **Developer-Friendly** - Minimal friction for projects already using Swagger/OpenAPI
- **Secure** - Input validation, sanitized outputs, and support for human-in-the-loop confirmations

## What's New in 1.3.2

- **Critical Complex Object Fix** - Fixed major bug where complex objects like `Workflow3ExecutionRequest` failed with JsonElement conversion errors
- **Enhanced Test Coverage** - Added 6 new test methods specifically for complex object deserialization scenarios
- **Improved Error Handling** - Better error messages and logging for complex object deserialization failures

## What's New in 1.3.1

- **Aligned with MCPBuckle 1.5.1** - Updated to use the latest MCPBuckle version
- **Added Comprehensive Test Suite** - First test suite with 53 tests covering all functionality

## What's New in 1.3.0

- **Full MCP Protocol Support** - MCPInvoke now implements the complete MCP protocol specification:
  - `initialize` - Returns server capabilities with protocol version 2025-06-18
  - `notifications/initialized` - Acknowledges client initialization
  - `tools/list` - Returns the list of available tools with proper JSON Schema
  - `tools/call` - Executes tool methods (existing functionality)
- **Claude Code CLI Integration** - Fully compatible with Anthropic's Claude Code CLI
- **JSON Schema Compliance** - Fixed schema generation to comply with JSON Schema draft 2020-12
- **Improved Error Handling** - Better error messages and protocol-compliant error responses

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

## Claude Code CLI Integration

MCPInvoke 1.3.0+ is fully compatible with Anthropic's Claude Code CLI. To integrate your API with Claude:

1. **Configure your API with MCPInvoke**:
   ```csharp
   // In Program.cs
   builder.Services.AddMcpInvokeWithControllers();
   app.UseMcpInvoke("/mcpinvoke");
   ```

2. **Add your API as an MCP server in Claude Code CLI**:
   ```bash
   claude mcp add --transport http my-api http://localhost:5000/mcpinvoke
   ```

3. **Use your API tools in Claude**:
   ```bash
   echo "Use the mcp__my-api__ToolName to perform task X" | claude
   ```

Your API tools will be available with the prefix `mcp__<server-name>__` in Claude Code CLI.

## MCP Protocol Support

MCPInvoke implements the full MCP protocol specification:

| Method | Description | Response |
|--------|-------------|----------|
| `initialize` | Client handshake | Server capabilities and protocol version |
| `notifications/initialized` | Client ready notification | Acknowledgment |
| `tools/list` | Request available tools | List of tool definitions with JSON schemas |
| `tools/call` | Execute a tool | Tool execution result |

The server supports protocol version `2025-06-18` and is compatible with all MCP-compliant clients.
