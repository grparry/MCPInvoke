# MCPInvoke Changelog

## [1.3.3] - 2025-07-22

### Fixed
- **Claude Code CLI Content Schema Compliance** - Fixed critical MCP response format to comply with Claude Code CLI content schema requirements
  - MCPInvoke now properly formats all tool responses as `{"content": [{"type": "text", "text": "stringified_json_data"}]}`
  - Resolves Zod validation errors from Claude Code CLI expecting specific MCP content format
  - Fixed issues where responses returned as raw arrays instead of proper MCP content objects
- **Universal MCP Client Compatibility** - Enhanced compatibility with all MCP clients that expect content schema format

### Added
- **Content Schema Formatting** - All tool execution results are now automatically wrapped in proper MCP content schema format
- **Enhanced Logging** - Added logging for MCP content schema formatting process for better debugging

### Technical Details
- Updated `McpExecutionService.cs` lines 774-789 to wrap all results in MCP content format
- Maintains full backward compatibility while adding standards compliance
- Ensures seamless integration with Claude Code CLI and other MCP-compliant clients

## [1.3.2] - 2025-07-22

### Fixed
- **Complex Object Deserialization** - Fixed critical bug where complex objects (like `BusinessProcessRequest`) were failing with "Object of type 'System.Text.Json.JsonElement' cannot be converted to type 'X'" error
  - MCPInvoke now properly uses the actual parameter type from method signatures instead of generic mapped types for complex object deserialization
  - Improved handling of nested objects, enums within complex objects, and type hierarchy scenarios
  - Enhanced logging to show when parameter type correction is applied for better debugging
- **Enhanced Error Reporting** - Added more detailed error messages for complex object deserialization failures with specific type information and raw JSON content

### Added
- **Comprehensive Complex Object Test Suite** - Added 6 new test methods specifically covering complex object deserialization scenarios:
  - `ProcessRequestAsync_ComplexObject_DeserializesCorrectly` - Basic complex objects with enums
  - `ProcessRequestAsync_WorkflowExecutionRequest_ReplicatesBugScenario` - Direct test of reported bug scenario
  - `ProcessRequestAsync_NestedComplexObject_DeserializesCorrectly` - Nested object hierarchies
  - `ProcessRequestAsync_EnumParameter_DeserializesCorrectly` - Enum parameter handling
  - `ProcessRequestAsync_ComplexObjectTypeMapMismatch_UsesPararameterTypeNotMappedType` - Core fix validation

### Technical Details
- Fixed parameter binding logic in `McpExecutionService.cs` lines 592-603 to prioritize `paramInfo.ParameterType` over `targetType` for complex objects
- This resolves scenarios where JSON schema mapping resulted in generic `object` types instead of specific strongly-typed classes
- Maintains backward compatibility with all existing functionality

## [1.3.1] - 2025-07-17

### Added
- **Comprehensive Test Suite** - Added 53 tests covering all functionality

### Changed
- **Aligned with MCPBuckle 1.5.1** - Updated dependency to MCPBuckle 1.5.1

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