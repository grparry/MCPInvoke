# MCPInvoke

MCPInvoke is a complete Model Context Protocol (MCP) server implementation for ASP.NET Core applications. Originally designed as a companion to MCPBuckle for tool execution, MCPInvoke now provides full MCP protocol support including tool discovery and execution.

## Purpose

This library enables ASP.NET Core applications with existing REST APIs and Swagger/OpenAPI documentation to easily become MCP-enabled. It provides a complete MCP server implementation that handles both tool discovery and execution, making your APIs accessible to AI agents and tools like Claude Code CLI.

## Features

- **Attribute-Driven (Optional, Extensible)** - Leverages existing OpenAPI/Swagger metadata by default, with optional MCP-specific attributes for customization
- **Standard-Compliant** - Implements JSON-RPC 2.0 and MCP specifications
- **Developer-Friendly** - Minimal friction for projects already using Swagger/OpenAPI
- **Secure** - Input validation, sanitized outputs, and support for human-in-the-loop confirmations

## What's New in 2.0.0

### 🚀 Major Enhancement - Enhanced Parameter Binding and OSS Preparation

- **Enhanced Parameter Binding Service** - Complete rewrite with schema-aware parameter binding that mirrors ASP.NET Core logic exactly
  - Runtime parameter source detection with comprehensive validation
  - Advanced route parameter analysis and intelligent error handling  
  - Schema-aware parameter binding with multi-framework support (net6.0-net9.0)
  - Complete integration with MCPBuckle 2.0.0's enhanced parameter source detection

- **MCPBuckle 2.0.0 Integration** - Full compatibility with enhanced parameter binding capabilities
  - Seamless integration with MCPBuckle 2.0.0's advanced parameter source detection
  - Coordinated v2.0.0 release for complete MCP tool discovery and execution
  - Enhanced schema generation with comprehensive route template analysis
  - Advanced parameter validation and intelligent error handling

### 🧹 OSS Preparation and Generic Test Patterns

- **Removed Proprietary Test Patterns** - Eliminated AnalyticsAPI-specific test content (1,106 lines)
  - Comprehensive cleanup of proprietary financial domain test patterns
  - Created generic web API patterns for e-commerce/business domains  
  - Enhanced documentation with pattern conversion guides
  - Clean separation of proprietary vs. OSS-ready content

- **137/137 Tests Passing** - Complete test coverage maintained across all frameworks
  - Enhanced parameter binding validation tests
  - MCPBuckle v2.0 integration tests  
  - Generic web API pattern demonstration tests
  - Multi-framework compatibility validation

### 🔧 Enhanced Schema Generation

- **Real-World API Support** - Complex query parameters with inheritance properly expanded with required validation attributes propagated across inheritance chains
- **Complete Parameter Schemas** - Both fixes work together to provide comprehensive parameter expansion for inheritance-aware processing
- **Seamless Upgrade** - Drop-in replacement for previous MCPInvoke versions with 100% backward compatibility

## What's New in 1.5.0 (Superseded by 2.0.0)

- **MCPBuckle 1.6.1 Integration** - Updated to use MCPBuckle 1.6.1 with circular reference protection and enhanced stability
- **Circular Reference Protection** - Prevents stack overflow errors when processing complex object schemas with self-referencing or mutually-referencing types
- **Enhanced Schema Reliability** - Improved robustness of complex object schema generation for production use
- **Full Test Suite Validation** - All 103 tests passing with the updated dependency

## What's New in 1.4.0

- **Comprehensive Schema Generation Overhaul** - Complete redesign of MCP tool definition schema generation achieving 100% test success (103/103 tests)
- **Route Parameter Extraction** - Automatic extraction and classification of route parameters from ASP.NET Core route templates like `{stepDefinitionId}`
- **Complex Object Introspection** - Full recursive schema generation for complex object parameters with proper Properties field population
- **Enhanced Parameter Detection** - Accurate identification of parameter sources (route, body, query, header) with proper MCP annotations
- **Method Documentation Support** - Automatic extraction of method descriptions from `DescriptionAttribute` and `DisplayNameAttribute`
- **Production-Ready Performance** - Schema generation optimized and validated for real-world applications

## What's New in 1.3.3

- **Claude Code CLI Content Schema Fix** - Fixed critical MCP response format to comply with Claude Code CLI content schema requirements
- **MCP Content Format Compliance** - All tool responses now properly formatted as `{"content": [{"type": "text", "text": "json_data"}]}`
- **Full Claude Code CLI Compatibility** - MCPInvoke now works seamlessly with Claude Code CLI and other MCP clients expecting content schema format

## What's New in 1.3.2

- **Critical Complex Object Fix** - Fixed major bug where complex objects like `BusinessProcessRequest` failed with JsonElement conversion errors
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

MCPInvoke 2.0.0+ is fully compatible with Anthropic's Claude Code CLI. To integrate your API with Claude:

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
