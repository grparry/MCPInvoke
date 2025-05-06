# MCPInvoke: Custom MCP Execution Handler Plan (Companion to MCPBuckle)

This document outlines the plan to build `MCPInvoke`, a custom MCP execution endpoint, as a companion to the `MCPBuckle` library (which handles MCP discovery). `MCPInvoke` will manage the execution of tools defined by MCP requests.

## Key Requirements and Design Points

1.  **Attribute-Driven (Optional, Extensible):**
    *   The handler supports MCP-specific attributes like `[McpServerToolType]` and `[McpServerTool(Name = "YourToolName")]`, but these are **optional**.
    *   By default, all necessary metadata (tool name, description, input schema, tags) is extracted from existing OpenAPI/Swagger attributes and XML comments. No MCP-specific tagging is required unless the project author needs to override or extend default behavior.
    *   If present, MCP-specific attributes take precedence and allow for custom tool naming, schema overrides, or additional metadata.

2.  **Endpoint:**
    *   Implement an ASP.NET Core endpoint (e.g., using `MapPost` in `Program.cs` or a dedicated controller).
    *   The endpoint listens at `/mcp` (or a configurable base path), following MCP protocol and transport layer recommendations (JSON-RPC 2.0 over HTTP POST, with optional SSE for real-time updates (post-V1)). Additional operational parameters (e.g., default logging levels, feature flags) should be configurable via standard ASP.NET Core configuration mechanisms.

3.  **Request/Response Cycle (MCP Standard):**
    *   **Deserialize:** Parse the incoming JSON MCP request, which conforms to JSON-RPC 2.0.
    *   **Identify:** Map the `tool_name` (from the request's `method` field, as defined in the MCP context provided by `MCPBuckle`) to the corresponding C# `MethodInfo` and controller/service type required for execution.
    *   **Parameter Binding:** Bind `tool_input` (from the request's `params`) to the method's parameters. This includes handling various C# types (primitives, complex objects, collections) and validating against the tool's JSON Schema. Conversion and validation errors will result in a structured JSON-RPC error, detailing specific failures (e.g., field name, error type) to aid client-side debugging.
    *   **Invoke:** Execute the method (supporting both sync and async), using dependency injection for service resolution.
    *   **Serialize:** Construct a JSON-RPC response, populating `tool_output` (success) or `tool_error` (failure). This must support all MCP result content types (text, image, resource, etc.). For non-textual content types like image or resource, a clear strategy (e.g., base64 encoding for inline data, or providing a resolvable URI) for their representation within the JSON-RPC `tool_output` will be defined.

4.  **Tool Definition Consumption & Registry:**
    *   **Tool Definition Consumption:** On application startup, the handler will obtain the list of available MCP tools and their definitions (including `name`, `description`, and `inputSchema`) from the `MCPBuckle` library (e.g., by invoking `MCPBuckle`'s `IContextGenerator` or `IControllerDiscoveryService`).
    *   **Internal Tool Registry:** Build an internal registry mapping each `tool_name` (provided by `MCPBuckle`) to the precise C# `MethodInfo` and its containing controller/service type, along with any pre-parsed parameter information necessary for invocation. This registry is populated using the information retrieved from `MCPBuckle` at application startup and is considered static for the lifecycle of the application instance. The MCP `listChanged` notification is not anticipated to be emitted by `MCPInvoke` in response to changes in the toolset from `MCPBuckle`, as the toolset is loaded once.
    *   **Metadata Utilization:** Utilize all metadata provided by `MCPBuckle` for each tool. This includes any preserved OpenAPI tags, summaries, and custom extensions (often found in an `annotations` field in the `MCPBuckle`-generated tool definition), ensuring full metadata fidelity can be leveraged during execution if needed (e.g., for custom logic based on annotations).

5.  **Execution Logic & Error Handling:**
    *   **Lookup:** Use the registry to locate the method for the incoming tool call.
    *   **Activation:** Resolve service instances via DI.
    *   **Parameter Binding:** Validate and convert input using the tool's JSON Schema.
    *   **Invocation:** Call the method, supporting both synchronous and asynchronous execution.
    *   **Error Handling:**
        - Protocol errors (unknown tool, invalid arguments, server errors) are returned as JSON-RPC errors.
        - Tool execution errors are returned in the result with `isError: true` and explanatory content, as per MCP standard.
    *   **Security:**
        - Validate all tool inputs.
        - Implement access controls and rate limiting.
        - Sanitize outputs and log all tool invocations.
        - Support human-in-the-loop confirmation for sensitive operations. This may involve MCPInvoke returning a specific MCP response payload indicating that a human confirmation step is required. The MCP client would then facilitate this confirmation (e.g., by prompting the user) before potentially re-issuing the request with an added confirmation token or flag.

## Extensibility & Best Practices

- **Minimal Developer Friction:**
    *   Projects already exposing Swagger endpoints do not need to add MCP-specific attributes or tags. All required metadata is extracted from existing OpenAPI/Swagger definitions.
    *   MCP-specific attributes are available for advanced scenarios (custom tool names, schema overrides, extra metadata), but are not required for standard operation.
    *   Any OpenAPI fields not directly mappable to MCP are preserved in the `annotations` or `x-openapi-original` fields.

- **Schema and Metadata Mapping:**
    *   OpenAPI `operationId` → MCP `name`
    *   `summary`/`description` → MCP `description`
    *   `parameters`, `requestBody` → MCP `inputSchema` (JSON Schema)
    *   `tags` → MCP `tags` (in `annotations`)
    *   All other OpenAPI metadata is preserved in `annotations` for full fidelity.

- **Protocol Compliance:**
    *   Strictly follow JSON-RPC 2.0 for all protocol messages.
    *   Support all MCP result content types (text, image, resource, etc.).
    *   Emit `listChanged` notifications if the underlying toolset (as managed and updated by `MCPBuckle`) changes at runtime and this notification is required for MCP clients.

- **Security and Observability:**
    *   Validate all inputs, implement access controls and rate limiting.
    *   Log all tool invocations and errors for audit and debugging.
    *   Provide human-in-the-loop UX for sensitive operations.

- **Testing and Documentation:**
    *   Document how to use MCP attributes for custom/advanced scenarios.

## Testing Strategy

A comprehensive testing strategy will be crucial to ensure the correctness, robustness, and compliance of `MCPInvoke`. The strategy will encompass unit, integration, and potentially end-to-end tests, focusing on the following key areas:

1.  **Protocol Compliance:**
    *   Validate strict adherence to JSON-RPC 2.0 for request parsing and response formatting across various scenarios (valid requests, malformed requests, different `id` types, etc.).
    *   Ensure compliance with MCP standards for `tool_name`, `tool_input`, `tool_output`, and `tool_error` structures.
    *   Verify correct handling of all MCP-defined result content types, especially the serialization of non-textual content.

2.  **Core Execution Logic:**
    *   **Tool Identification:** Test the mapping of `tool_name` to the correct C# `MethodInfo` from the internal registry.
    *   **Parameter Binding & Validation:**
        *   Cover various C# parameter types (primitives, complex objects, collections, nullable types) and their mapping from `tool_input`.
        *   Test validation against the JSON Schema, including required fields, type mismatches, format constraints, etc.
        *   Ensure clear and structured error reporting for binding and validation failures.
    *   **Method Invocation:** Verify correct invocation of both synchronous and asynchronous tool methods, including DI resolution for services.
    *   **Result Serialization:** Test successful serialization of `tool_output` and correct formatting of `tool_error` for both protocol and tool execution errors.

3.  **Integration with `MCPBuckle`:**
    *   Test the consumption of tool definitions provided by `MCPBuckle` at startup.
    *   Ensure the internal tool registry is correctly populated based on `MCPBuckle`'s output.

4.  **Error Handling:**
    *   Thoroughly test all defined error paths:
        *   Unknown tool.
        *   Invalid/missing parameters.
        *   Tool execution exceptions (both anticipated and unhandled).
        *   Internal server errors within `MCPInvoke`.
    *   Verify that errors are consistently reported as per JSON-RPC and MCP specifications.

5.  **Security Mechanisms (as implemented):**
    *   Test input validation logic to prevent common vulnerabilities.
    *   If/when access controls are added, test their enforcement.
    *   Verify the human-in-the-loop confirmation flow, ensuring correct state management and response/request patterns.

6.  **Endpoint Functionality:**
    *   Test the ASP.NET Core endpoint for correct routing, HTTP method handling (POST), and content type negotiation.
    *   Verify configurability of the endpoint path and other operational parameters.

## Goal

This approach provides robust, standards-compliant MCP tool execution with minimal developer friction through `MCPInvoke`. It complements MCPBuckle’s discovery capabilities, enabling seamless integration for projects already using Swagger/OpenAPI, while supporting advanced customization when needed via optional MCP-specific attributes.
