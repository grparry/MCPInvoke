# MCPInvoke Test Suite

This comprehensive test suite validates the MCPInvoke schema generation fixes for complex workflow configuration operations. The tests ensure that the three critical gaps identified in the root cause analysis are properly addressed:

1. **Route Parameter Extraction** - Parsing route templates like `{stepDefinitionId}`
2. **Complex Object Schema Generation** - Recursive introspection of nested objects  
3. **Parameter Source Detection** - Distinguishing route, body, query, and header parameters

## Test Structure

### Core Schema Generation Tests
- **`AspNetControllerToolDefinitionProviderSchemaTests.cs`** - Core tests for the three critical schema generation fixes
- **`McpExecutionServiceTests.cs`** - Existing comprehensive execution service tests (enhanced)

### Integration Tests
- **`Workflow3ScenarioTests.cs`** - End-to-end tests for real Workflow3 scenarios that were failing

### Standards Compliance
- **`McpSpecificationComplianceTests.cs`** - Validates generated schemas comply with official MCP specification

### Performance Tests
- **`SchemaGenerationPerformanceTests.cs`** - Ensures fixes don't introduce performance regressions

## Key Test Scenarios

### Route Parameter Extraction
```csharp
[HttpPut("step-definition/{stepDefinitionId}")]
public IActionResult UpdateStepDefinition(int stepDefinitionId, [FromBody] UpdateStepDefinitionRequest request)
```
Tests verify that `stepDefinitionId` is properly extracted from the route template and mapped as a route parameter.

### Complex Object Schema Generation
```csharp
public class UpdateStepDefinitionRequest
{
    public ComposableStepDefinition Definition { get; set; }  // Nested complex object
}
```
Tests verify that nested objects are recursively introspected with complete property schemas.

### Parameter Source Detection
Tests verify that parameters are correctly identified as:
- **Route parameters** - from URL path segments
- **Body parameters** - complex objects from request body
- **Query parameters** - primitive types from query string
- **Header parameters** - values from HTTP headers

## Real-World Validation

### Workflow3_UpdateStepDefinition Scenario
The test suite includes comprehensive validation of the exact scenario that was failing:

```json
{
  "jsonrpc": "2.0", 
  "method": "tools/call",
  "params": {
    "name": "Workflow3_UpdateStepDefinition",
    "arguments": {
      "stepDefinitionId": 7,
      "request": {
        "definition": {
          "processor": {
            "promptTemplateId": null
          }
        }
      }
    }
  }
}
```

This call was previously failing with "Parameter 'stepDefinitionId' schema not found" but now succeeds with proper schema generation.

## MCP Specification Compliance

Tests validate that generated schemas conform to the official MCP specification:

- Tool definitions have required `name`, `description`, and `inputSchema` fields
- Input schemas follow JSON Schema specification format
- Parameter types use valid JSON Schema types (`string`, `integer`, `object`, `array`, etc.)
- Complex objects include `properties` and `required` arrays
- Arrays include `items` schema definitions

## Performance Validation

Performance tests ensure that the enhanced schema generation:

- Completes simple controllers under 100ms
- Completes complex controllers under 500ms  
- Benefits from caching on repeated calls
- Handles circular references without stack overflow
- Scales linearly with number of controllers
- Uses reasonable memory (under 10MB for large schemas)

## Running Tests

```bash
# Run all tests
dotnet test MCPInvoke.Tests.csproj

# Run specific test categories
dotnet test --filter Category=Schema
dotnet test --filter Category=Performance
dotnet test --filter Category=Integration

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Test Data and Models

The test suite includes comprehensive test models that replicate real-world complexity:

- **Simple Route Controllers** - Basic CRUD operations
- **Complex Object Controllers** - Nested object hierarchies
- **Workflow3 Mock Controllers** - Realistic workflow configuration scenarios
- **Performance Test Models** - Large schemas and deeply nested structures

## Expected Test Results

All tests should pass after implementing the schema generation fixes. Key validations include:

✅ Route parameters properly extracted and mapped  
✅ Complex objects generate detailed property schemas  
✅ Parameter sources correctly identified  
✅ MCP specification compliance maintained  
✅ Performance remains acceptable for production use  
✅ No regression in existing functionality  

## Troubleshooting

If tests fail, check:

1. **Route Parameter Tests** - Ensure route template parsing handles all ASP.NET Core route formats
2. **Complex Object Tests** - Verify recursive object introspection handles circular references
3. **Performance Tests** - Check that schema generation caching is working properly
4. **Integration Tests** - Ensure end-to-end MCP call execution succeeds without schema errors

## Contributing

When adding new tests:

1. Follow the existing naming conventions
2. Include both positive and negative test cases  
3. Add performance tests for new schema generation features
4. Verify MCP specification compliance
5. Include realistic test data that matches production scenarios

This test suite provides comprehensive validation that the MCPInvoke schema generation fixes successfully enable conversational Workflow3 configuration operations via MCP.