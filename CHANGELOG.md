# MCPInvoke Changelog

## [1.4.0] - 2025-08-07

### Major Enhancements
- **Comprehensive Schema Generation Improvements** - Complete overhaul of MCP tool definition schema generation with 100% test success rate (103/103 tests passing)
  - **Route Parameter Extraction** - Automatic extraction of route parameters from ASP.NET Core route templates like `{stepDefinitionId}`, `{tenantId}`, etc.
  - **Complex Object Schema Generation** - Full recursive introspection of complex object parameters with Properties field population
  - **Parameter Source Detection** - Proper identification and annotation of parameter sources (route, body, query, header)
  - **Enhanced Properties Support** - Both Properties field and Annotations are properly populated for complex objects
  - **Method Description Enhancement** - Support for `DescriptionAttribute` and `DisplayNameAttribute` for better tool documentation

### Fixed
- **Complex Object Schema Properties** - Fixed Properties field generation for complex object parameters with recursive property introspection
- **Route Parameter Detection** - Enhanced route template parsing to correctly extract and classify route parameters
- **Case Sensitivity Issues** - Fixed test assertions to match C# PascalCase property naming conventions
- **Array Property Schema Generation** - Corrected schema generation for array and collection properties within complex objects
- **Enum Property Handling** - Improved enum property schema generation with proper type and constraint detection
- **Nested Object Processing** - Enhanced handling of deeply nested object structures in schema generation
- **MCP Error Code Compliance** - Fixed error codes to match MCP specification (-32602 for invalid params vs -32603 for internal errors)
- **Performance Test Assembly Scanning** - Resolved assembly scanning issues in performance tests by adding proper test assembly parameters

### Technical Improvements
- **Enhanced AspNetControllerToolDefinitionProvider** - Major improvements to `GenerateComplexObjectSchema` method for comprehensive object introspection
- **Improved McpExecutionService** - Better parameter binding error detection and MCP-compliant error responses
- **Comprehensive Test Coverage** - All 103 tests now passing, including complex schema generation, integration tests, and performance tests
- **Method Signature Analysis** - Enhanced analysis of controller method signatures for accurate parameter type detection
- **Circular Reference Handling** - Robust handling of circular references in complex object schemas to prevent stack overflow

### Developer Experience
- **100% Test Success Rate** - All functionality verified through comprehensive test suite covering edge cases and real-world scenarios
- **Performance Validated** - Schema generation performance tested and optimized for production use
- **Real-World Integration** - Enhanced compatibility with complex ASP.NET Core applications and Workflow3-style controllers

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