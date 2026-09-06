// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

public sealed class ContentPartTests
{
    [Fact]
    public void TextPart_WhenTextIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new TextPart(null!, TextSemantics.Plain, ExtensionData.Empty));

        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void TextPart_WhenArgumentsAreValid_ExposesValues()
    {
        var part = new TextPart("hi", TextSemantics.Markdown, ExtensionData.Empty);

        part.Text.ShouldBe("hi");
        part.Semantics.ShouldBe(TextSemantics.Markdown);
        part.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void AnyContentPart_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new TextPart("hi", TextSemantics.Plain, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolCallPart_WhenToolIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            null!,
            default,
            null,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("tool");
    }

    [Fact]
    public void ToolCallPart_WhenArgumentsAreValid_ExposesValues()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("read"), null, "read");

        var part = new ToolCallPart(callId, tool, default, null, ExtensionData.Empty);

        part.CallId.ShouldBe(callId);
        part.Tool.ShouldBe(tool);
        part.ProviderCallId.ShouldBeNull();
    }

    [Fact]
    public void ToolResultPart_WhenContentIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(new ToolId("read"), null, "read"),
            new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
            default,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ToolResultPart_WhenOutcomeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(new ToolId("read"), null, "read"),
            null!,
            [],
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void StructuredDataPart_WhenSchemaIsNull_Succeeds()
    {
        var part = new StructuredDataPart(default, null, ExtensionData.Empty);

        part.Schema.ShouldBeNull();
    }

    [Fact]
    public void UnknownContentPart_WhenTypeNameIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new UnknownContentPart("   ", default, ExtensionData.Empty));

        exception.ParamName.ShouldBe("typeName");
    }

    [Fact]
    public void MediaReferencePart_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new MediaReferencePart(null!, MediaSemantics.Input, ExtensionData.Empty));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ReasoningPart_WhenContentIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ReasoningPart(null!, ExtensionData.Empty));

        exception.ParamName.ShouldBe("content");
    }
}
