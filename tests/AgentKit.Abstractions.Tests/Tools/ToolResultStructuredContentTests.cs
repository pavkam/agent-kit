// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies ToolResultStructuredContent behavior and contracts.</summary>
public sealed class ToolResultStructuredContentTests
{
    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenSourceDisposed_OwnsStructuralValue()
    {
        ToolResultStructuredContent content;
        using (var document = JsonDocument.Parse("{\"b\":2,\"a\":[true]}"))
        {
            content = new ToolResultStructuredContent(document.RootElement, null, ExtensionData.Empty);
        }

        using var equivalent = JsonDocument.Parse("{\"b\":2,\"a\":[true]}");
        var same = new ToolResultStructuredContent(equivalent.RootElement, null, ExtensionData.Empty);
        content.Value.GetProperty("a")[0].GetBoolean().ShouldBeTrue();
        content.ShouldBe(same);
        content.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultStructuredContent(default, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        using var document = JsonDocument.Parse("{}");
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultStructuredContent(document.RootElement, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenSchemaVersionIsDefault_ThrowsExactException()
    {
        using var document = JsonDocument.Parse("{}");
        var schema = new JsonSchemaReference("schema-name", default);
        var exception = Should.Throw<ArgumentException>(() => new ToolResultStructuredContent(document.RootElement, schema, ExtensionData.Empty));
        exception.ParamName.ShouldBe("schema");
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenSchemaIsValid_RetainsSchema()
    {
        using var document = JsonDocument.Parse("{}");
        var schema = new JsonSchemaReference("schema-name", new SchemaVersion("1"));
        var content = new ToolResultStructuredContent(document.RootElement, schema, ExtensionData.Empty);
        content.Schema.ShouldBe(schema);
        content.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        using var document = JsonDocument.Parse("{}");
        var original = new ToolResultStructuredContent(document.RootElement, null, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
