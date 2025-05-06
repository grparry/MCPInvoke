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

*Documentation coming soon*
