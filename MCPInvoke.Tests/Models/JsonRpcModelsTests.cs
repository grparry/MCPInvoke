using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using MCPInvoke.Models;
using MCPInvoke;

namespace MCPInvoke.Tests.Models
{
    public class JsonRpcModelsTests
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonRpcModelsTests()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        [Fact]
        public void JsonRpcRequest_DeserializesCorrectly()
        {
            // Arrange
            var json = @"{
                ""jsonrpc"": ""2.0"",
                ""method"": ""test/method"",
                ""params"": { ""key"": ""value"" },
                ""id"": 123
            }";

            // Act
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(json, _jsonOptions);

            // Assert
            Assert.NotNull(request);
            Assert.Equal("2.0", request.JsonRpcVersion);
            Assert.Equal("test/method", request.Method);
            Assert.NotNull(request.Params);
            Assert.Equal(123, request.Id?.GetInt32());
        }

        [Fact]
        public void JsonRpcRequest_HandlesNullId()
        {
            // Arrange
            var json = @"{
                ""jsonrpc"": ""2.0"",
                ""method"": ""notification"",
                ""params"": { }
            }";

            // Act
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(json, _jsonOptions);

            // Assert
            Assert.NotNull(request);
            Assert.Null(request.Id);
        }

        [Fact]
        public void JsonRpcRequest_HandlesStringId()
        {
            // Arrange
            var json = @"{
                ""jsonrpc"": ""2.0"",
                ""method"": ""test"",
                ""id"": ""string-id""
            }";

            // Act
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(json, _jsonOptions);

            // Assert
            Assert.NotNull(request);
            Assert.Equal("string-id", request.Id?.GetString());
        }

        [Fact]
        public void JsonRpcResponse_SerializesCorrectlyWithResult()
        {
            // Arrange
            var response = new JsonRpcResponse(
                JsonDocument.Parse("123").RootElement,
                result: new { message = "success" });

            // Act
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var parsed = JsonDocument.Parse(json);

            // Assert
            Assert.Equal("2.0", parsed.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal(123, parsed.RootElement.GetProperty("id").GetInt32());
            Assert.Equal("success", parsed.RootElement.GetProperty("result").GetProperty("message").GetString());
            Assert.False(parsed.RootElement.TryGetProperty("error", out _));
        }

        [Fact]
        public void JsonRpcResponse_SerializesCorrectlyWithError()
        {
            // Arrange
            var response = new JsonRpcResponse(
                JsonDocument.Parse("\"abc\"").RootElement,
                error: new JsonRpcError { Code = -32602, Message = "Invalid params", Data = new { details = "Missing required field" } });

            // Act
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var parsed = JsonDocument.Parse(json);

            // Assert
            Assert.Equal("2.0", parsed.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal("abc", parsed.RootElement.GetProperty("id").GetString());
            Assert.False(parsed.RootElement.TryGetProperty("result", out _));
            
            var error = parsed.RootElement.GetProperty("error");
            Assert.Equal(-32602, error.GetProperty("code").GetInt32());
            Assert.Equal("Invalid params", error.GetProperty("message").GetString());
            Assert.Equal("Missing required field", error.GetProperty("data").GetProperty("details").GetString());
        }

        [Fact]
        public void JsonRpcResponse_HandlesNullId()
        {
            // Arrange
            var response = new JsonRpcResponse(null, result: "test");

            // Act
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var parsed = JsonDocument.Parse(json);

            // Assert
            Assert.True(parsed.RootElement.GetProperty("id").ValueKind == JsonValueKind.Null);
        }

        [Fact]
        public void JsonRpcError_SerializesCorrectly()
        {
            // Arrange
            var error = new JsonRpcError { Code = -32700, Message = "Parse error", Data = "Unexpected character at position 42" };

            // Act
            var json = JsonSerializer.Serialize(error, _jsonOptions);
            var parsed = JsonDocument.Parse(json);

            // Assert
            Assert.Equal(-32700, parsed.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("Parse error", parsed.RootElement.GetProperty("message").GetString());
            Assert.Equal("Unexpected character at position 42", parsed.RootElement.GetProperty("data").GetString());
        }

        [Fact]
        public void JsonRpcError_HandlesNullData()
        {
            // Arrange
            var error = new JsonRpcError { Code = -32601, Message = "Method not found", Data = null };

            // Act
            var json = JsonSerializer.Serialize(error, _jsonOptions);
            var parsed = JsonDocument.Parse(json);

            // Assert
            Assert.Equal(-32601, parsed.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("Method not found", parsed.RootElement.GetProperty("message").GetString());
            Assert.False(parsed.RootElement.TryGetProperty("data", out _));
        }

        [Fact]
        public void McpParameterInfo_DefaultValues()
        {
            // Arrange & Act
            var param = new McpParameterInfo();

            // Assert
            Assert.Equal(string.Empty, param.Name); // Default is empty string
            Assert.Equal(string.Empty, param.Type); // Default is empty string
            Assert.Null(param.Description); // Default is null (nullable)
            Assert.False(param.IsRequired);
        }

        [Fact]
        public void McpParameterInfo_InitializedCorrectly()
        {
            // Arrange & Act
            var param = new McpParameterInfo
            {
                Name = "testParam",
                Type = "string",
                Description = "A test parameter",
                IsRequired = true
            };

            // Assert
            Assert.Equal("testParam", param.Name);
            Assert.Equal("string", param.Type);
            Assert.Equal("A test parameter", param.Description);
            Assert.True(param.IsRequired);
        }

        [Fact]
        public void McpToolDefinition_DefaultValues()
        {
            // Arrange & Act
            var tool = new McpToolDefinition();

            // Assert
            Assert.Equal(string.Empty, tool.Name);
            Assert.Null(tool.Description);
            Assert.Equal(string.Empty, tool.HandlerTypeAssemblyQualifiedName);
            Assert.Equal(string.Empty, tool.MethodName);
            Assert.NotNull(tool.InputSchema);
            Assert.Empty(tool.InputSchema);
        }

        [Fact]
        public void McpToolDefinition_InitializedCorrectly()
        {
            // Arrange & Act
            var tool = new McpToolDefinition
            {
                Name = "TestTool",
                Description = "A test tool",
                HandlerTypeAssemblyQualifiedName = "TestNamespace.TestClass, TestAssembly",
                MethodName = "TestMethod",
                InputSchema = new List<McpParameterInfo>
                {
                    new McpParameterInfo { Name = "param1", Type = "string" },
                    new McpParameterInfo { Name = "param2", Type = "number" }
                }
            };

            // Assert
            Assert.Equal("TestTool", tool.Name);
            Assert.Equal("A test tool", tool.Description);
            Assert.Equal("TestNamespace.TestClass, TestAssembly", tool.HandlerTypeAssemblyQualifiedName);
            Assert.Equal("TestMethod", tool.MethodName);
            Assert.Equal(2, tool.InputSchema.Count);
        }
    }
}