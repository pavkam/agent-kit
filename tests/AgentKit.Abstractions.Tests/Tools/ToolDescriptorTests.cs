// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;

public sealed class ToolDescriptorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenNameInvalid_ThrowsArgumentException(string? name) => _ = Should.Throw<ArgumentException>(() => Create(name: name!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDescriptionInvalid_ThrowsArgumentException(string? description) => _ = Should.Throw<ArgumentException>(() => Create(description: description!));

    [Fact]
    public void Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolDescriptor(
            new ToolId("t"), null, "tool", "description", default, ToolEffect.ReadOnly, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var descriptor = Create();

        descriptor.Id.ShouldBe(new ToolId("t"));
        descriptor.Version.ShouldBeNull();
        descriptor.Name.ShouldBe("tool");
        descriptor.Description.ShouldBe("description");
        descriptor.Effect.ShouldBe(ToolEffect.ReadOnly);
        descriptor.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Equality_WhenSameValues_InstancesAreEqual()
    {
        Create().ShouldBe(Create());
        Create().GetHashCode().ShouldBe(Create().GetHashCode());
    }

    [Fact]
    public void Equality_WhenDifferentName_InstancesAreNotEqual() =>
        Create(name: "tool").ShouldNotBe(Create(name: "other"));

    [Fact]
    public void Equality_WhenDifferentSchema_InstancesAreNotEqual() =>
        Create(schema: "{}").ShouldNotBe(Create(schema: /*lang=json,strict*/ """{"type":"object"}"""));

    [Fact]
    public void Equality_WhenBothSchemasUndefined_InstancesAreEqual()
    {
        var first = Create(schema: null);
        var second = Create(schema: null);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOneSchemaUndefined_InstancesAreNotEqual() =>
        Create(schema: null).ShouldNotBe(Create(schema: "{}"));

    private static ToolDescriptor Create(
        string? name = "tool", string? description = "description", string? schema = "{}") =>
        new(
            new ToolId("t"),
            null,
            name!,
            description!,
            schema is null ? default : JsonDocument.Parse(schema).RootElement,
            ToolEffect.ReadOnly, ExtensionData.Empty);
}
