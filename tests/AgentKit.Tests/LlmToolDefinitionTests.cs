// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Text.Json;

/// <summary>Verifies LlmToolDefinition behavior and contracts.</summary>
[Collection(AdmissionObservabilityGroup.Name)]
public sealed class LlmToolDefinitionTests
{
    [Fact]
    public void LlmToolDefinition_WhenSourceDocumentIsDisposed_RetainsConstructorAndInitializerSchemas()
    {
        LlmToolDefinition constructed;
        LlmToolDefinition initialized;
        using (var document = JsonDocument.Parse("{\"type\":\"object\"}"))
        {
            constructed = new LlmToolDefinition(new ToolId("constructed"), "constructed", null, document.RootElement);
            initialized = new LlmToolDefinition(new ToolId("initialized"), "initialized", null, default)
            {
                ParametersSchema = document.RootElement,
            };
        }

        constructed.ParametersSchema.GetProperty("type").GetString().ShouldBe("object");
        initialized.ParametersSchema.GetProperty("type").GetString().ShouldBe("object");
    }

    [Fact]
    public void LlmToolDefinition_WhenSchemasHaveEquivalentJsonContent_UsesEqualHashCodes()
    {
        var first = new LlmToolDefinition(new ToolId("test-tool"), "test_tool", "A test tool.", ParseSchema( /*lang=json,strict*/"{ \"name\": \"\\u0061\", \"count\": 1.0 }"));
        var second = new LlmToolDefinition(first.Id, first.Name, first.Description, ParseSchema( /*lang=json,strict*/"{\"count\":1,\"name\":\"a\"}"));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void LlmToolDefinition_WhenSchemasAreUndefined_UsesValueEqualityWithoutThrowing()
    {
        var first = new LlmToolDefinition(new ToolId("test-tool"), "test_tool", "A test tool.", default);
        var second = new LlmToolDefinition(first.Id, first.Name, first.Description, default);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static JsonElement ParseSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
