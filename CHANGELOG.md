# MCPInvoke Changelog

## [1.3.0] - 2025-01-03

### Added
- **Full MCP Protocol Support** - MCPInvoke now implements the complete MCP protocol specification:
  - `initialize` - Returns server capabilities with protocol version 2025-06-18
  - `notifications/initialized` - Acknowledges client initialization  
  - `tools/list` - Returns the list of available tools with proper JSON Schema
  - `tools/call` - Executes tool methods (existing functionality)
- **Claude Code CLI Integration** - Fully compatible with Anthropic's Claude Code CLI
- **JSON Schema Compliance** - Fixed schema generation to comply with JSON Schema draft 2020-12
- **Improved Error Handling** - Better error messages and protocol-compliant error responses

### Fixed
- Protocol version mismatch - Updated from 2025-05-06 to 2025-06-18
- JSON Schema validation errors - Removed 'required' from property level, kept only at object level  
- Null description handling - Filtered out null descriptions from tool schemas

### Changed
- MCPInvoke is now a complete MCP server, not just a tool execution endpoint
- Updated package description to reflect full MCP server capabilities

## [1.2.0] - Previous Release

### Added
- Enhanced API Response Handling - Improved handling of complex ASP.NET Core return types including ActionResult<T>, ObjectResult, and other IActionResult implementations
- MCPBuckle 1.5.0 Integration - Updated to use MCPBuckle 1.5.0 with enhanced schema generation, better parameter binding, and improved enum support
- Advanced Enum Support - Added full support for enum types, including both integer and string-based enums with JsonStringEnumConverter compatibility
- Collection Type Support - Better handling of array and collection return types for improved client compatibility
- New Middleware Architecture - Introduced dedicated McpInvokeMiddleware for more efficient request processing
- Simplified Configuration - Added ApplicationBuilderExtensions with UseMcpInvoke extension method for easier integration
- Parameter Annotations - Added support for additional parameter metadata through Annotations property

## [1.1.0] - Previous Release

### Added
- MCPBuckle 1.4.0 Integration - Updated to use the MCPBuckle 1.4.0 NuGet package for better MCP specification compliance
- Special JSON-RPC Method Handling - Added support for standard JSON-RPC methods:
  - `notifications/initialized` - Properly acknowledges initialization notifications from MCP clients
  - `tools/list` - Returns a helpful error message directing clients to use the MCP context endpoint instead