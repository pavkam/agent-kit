// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using System.Text.Json;

using AgentKit;

public sealed class ContentPartHierarchyEqualityTests
{
    [Fact]
    public void JsonSchemaReference_Constructor_WhenNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new JsonSchemaReference(" ", new SchemaVersion("1")));

    [Fact]
    public void JsonSchemaReference_Constructor_WhenValid_RoundTripsProperties()
    {
        var reference = new JsonSchemaReference("schema", new SchemaVersion("1"));

        reference.Name.ShouldBe("schema");
        reference.Version.ShouldBe(new SchemaVersion("1"));
    }

    [Fact]
    public void JsonSchemaReference_Equality_WhenSameValues_InstancesAreEqual() =>
        new JsonSchemaReference("schema", new SchemaVersion("1")).ShouldBe(
            new JsonSchemaReference("schema", new SchemaVersion("1")));

    [Fact]
    public void StructuredDataPart_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new StructuredDataPart(default, null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void StructuredDataPart_Equality_WhenSameValues_InstancesAreEqual() =>
        new StructuredDataPart(default, null, ExtensionData.Empty).ShouldBe(
            new StructuredDataPart(default, null, ExtensionData.Empty));

    [Fact]
    public void ReasoningContent_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ReasoningContent(null, ReasoningVisibility.Visible, null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContent_Equality_WhenSameValues_InstancesAreEqual() =>
        new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty).ShouldBe(
            new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty));

    [Fact]
    public void ReasoningPart_Constructor_WhenContentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningPart(null!, ExtensionData.Empty));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ReasoningPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var content = new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty);

        new ReasoningPart(content, ExtensionData.Empty).ShouldBe(new ReasoningPart(content, ExtensionData.Empty));
    }

    [Fact]
    public void MediaReferencePart_Constructor_WhenReferenceNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new MediaReferencePart(null!, MediaSemantics.Input, ExtensionData.Empty));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void MediaReferencePart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var reference = new MediaReference(
            new MediaId(Guid.NewGuid()), MediaSourceKind.Uri, "image/png", new Uri("https://example.com/a.png"),
            [], null, null, ExtensionData.Empty);

        new MediaReferencePart(reference, MediaSemantics.Input, ExtensionData.Empty).ShouldBe(
            new MediaReferencePart(reference, MediaSemantics.Input, ExtensionData.Empty));
    }

    [Fact]
    public void TextPart_Equality_WhenSameValues_InstancesAreEqual() =>
        new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty).ShouldBe(
            new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty));

    [Fact]
    public void ToolCallOutcome_Equality_WhenSameValues_InstancesAreEqual() =>
        new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty).ShouldBe(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty));

    [Fact]
    public void ToolReference_Constructor_WhenNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new ToolReference(new ToolId("t"), null, " "));

    [Fact]
    public void ToolReference_Equality_WhenSameValues_InstancesAreEqual() =>
        new ToolReference(new ToolId("t"), null, "tool").ShouldBe(new ToolReference(new ToolId("t"), null, "tool"));

    [Fact]
    public void ToolCallPart_Constructor_WhenToolNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolCallPart(new ToolCallId(Guid.NewGuid()), null!, default, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("tool");
    }

    [Fact]
    public void ToolCallPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");

        new ToolCallPart(callId, tool, default, null, ExtensionData.Empty).ShouldBe(
            new ToolCallPart(callId, tool, default, null, ExtensionData.Empty));
    }

    [Fact]
    public void ToolResultPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty);
        var content = ImmutableArray.Create<ContentPart>(new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty));

        var first = new ToolResultPart(callId, tool, outcome, content, ExtensionData.Empty);
        var second = new ToolResultPart(callId, tool, outcome, content, ExtensionData.Empty);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ToolResultPart_Equality_WhenDifferentContent_InstancesAreNotEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty);

        var first = new ToolResultPart(
            callId, tool, outcome, [new TextPart("a", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var second = new ToolResultPart(
            callId, tool, outcome, [new TextPart("b", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void UnknownContentPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var raw = JsonDocument.Parse("{}").RootElement;

        new UnknownContentPart("custom", raw, ExtensionData.Empty).ShouldBe(
            new UnknownContentPart("custom", raw, ExtensionData.Empty));
    }

    [Fact]
    public void MediaReference_Equality_WhenSameInlineBytes_InstancesAreEqual()
    {
        var first = new MediaReference(
            new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null,
            [1, 2, 3], null, null, ExtensionData.Empty);
        var second = new MediaReference(
            first.Id, MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], null, null, ExtensionData.Empty);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void MediaReference_Equality_WhenDifferentInlineBytes_InstancesAreNotEqual()
    {
        var id = new MediaId(Guid.NewGuid());
        var first = new MediaReference(
            id, MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], null, null, ExtensionData.Empty);
        var second = new MediaReference(
            id, MediaSourceKind.InlineBytes, "image/png", null, [4, 5, 6], null, null, ExtensionData.Empty);

        first.ShouldNotBe(second);
    }
}
