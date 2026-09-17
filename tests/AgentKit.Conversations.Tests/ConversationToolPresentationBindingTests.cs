// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationToolPresentationBinding behavior and contracts.</summary>
public sealed class ConversationToolPresentationBindingTests
{
    [Fact]
    public void Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var advertised = Advertised(new ToolId("read"), "read");

        var exception = Should.Throw<ArgumentNullException>(
            () => new ConversationToolPresentationBinding(null!, advertised));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Constructor_WhenAdvertisedToolIsNull_ThrowsArgumentNullException()
    {
        var descriptor = Descriptor(new ToolId("read"));

        var exception = Should.Throw<ArgumentNullException>(
            () => new ConversationToolPresentationBinding(descriptor, null!));

        exception.ParamName.ShouldBe("advertisedTool");
    }

    [Fact]
    public void Constructor_WhenDescriptorAndAdvertisedToolIdentifyDifferentTools_ThrowsArgumentException()
    {
        var descriptor = Descriptor(new ToolId("read"));
        var advertised = Advertised(new ToolId("write"), "write");

        var exception = Should.Throw<ArgumentException>(
            () => new ConversationToolPresentationBinding(descriptor, advertised));

        exception.ParamName.ShouldBe("advertisedTool");
    }

    [Fact]
    public void Constructor_WhenIdsMatch_SetsProperties()
    {
        var id = new ToolId("read");
        var descriptor = Descriptor(id);
        var advertised = Advertised(id, "read");

        var binding = new ConversationToolPresentationBinding(descriptor, advertised);

        binding.Descriptor.ShouldBeSameAs(descriptor);
        binding.AdvertisedTool.ShouldBeSameAs(advertised);
    }

    [Fact]
    public void Equals_WhenDescriptorAndAdvertisedToolMatch_ReturnsTrueWithMatchingHashCode()
    {
        var id = new ToolId("read");
        var descriptor = Descriptor(id);
        var advertised = Advertised(id, "read");
        var first = new ConversationToolPresentationBinding(descriptor, advertised);
        var second = new ConversationToolPresentationBinding(descriptor, advertised);

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenAdvertisedToolDiffers_ReturnsFalse()
    {
        var id = new ToolId("read");
        var descriptor = Descriptor(id);
        var first = new ConversationToolPresentationBinding(descriptor, Advertised(id, "read"));
        var second = new ConversationToolPresentationBinding(descriptor, Advertised(id, "read-alias"));

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeName()
    {
        var id = new ToolId("read");
        var binding = new ConversationToolPresentationBinding(Descriptor(id), Advertised(id, "read"));

        var text = binding.ToString();

        text.ShouldContain(nameof(ConversationToolPresentationBinding));
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var id = new ToolId("read");
        var original = new ConversationToolPresentationBinding(Descriptor(id), Advertised(id, "read"));

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }

    private static ToolDescriptor Descriptor(ToolId id)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new ToolDescriptor(
            id,
            new ToolVersion("v1"),
            "display name",
            "Describes a tool.",
            new JsonSchema(
                new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"),
                document.RootElement),
            null,
            new ToolEffects(ToolEffect.ReadOnly, IdempotencyClassification.ReadOnly, []),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("tests"),
            ExtensionData.Empty);
    }

    private static LlmToolDefinition Advertised(ToolId id, string name)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new LlmToolDefinition(id, name, "Describes a tool.", document.RootElement);
    }
}
