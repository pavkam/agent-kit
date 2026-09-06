// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

public sealed class SupportingValueTypeTests
{
    [Fact]
    public void MediaReference_WhenMediaTypeIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(
            new MediaId(Guid.NewGuid()),
            MediaSourceKind.InlineBytes,
            "   ",
            null,
            [],
            null,
            null,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("mediaType");
    }

    [Fact]
    public void MediaReference_WhenInlineBytesIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(
            new MediaId(Guid.NewGuid()),
            MediaSourceKind.InlineBytes,
            "image/png",
            null,
            default,
            null,
            null,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("inlineBytes");
    }

    [Fact]
    public void MediaReference_WhenArgumentsAreValid_ExposesValues()
    {
        var id = new MediaId(Guid.NewGuid());

        var reference = new MediaReference(
            id,
            MediaSourceKind.Uri,
            "image/png",
            new Uri("https://example.test/image.png"),
            [],
            1024,
            new ContentHash("abc123"),
            ExtensionData.Empty);

        reference.Id.ShouldBe(id);
        _ = reference.Uri.ShouldNotBeNull();
        reference.SizeInBytes.ShouldBe(1024);
    }

    [Fact]
    public void ToolReference_WhenNameIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolReference(new ToolId("read"), null, "   "));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void ToolCallOutcome_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolCallOutcome(ToolCallOutcomeKind.Success, null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void JsonSchemaReference_WhenNameIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new JsonSchemaReference("   ", new SchemaVersion("1")));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void ReasoningContent_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ReasoningContent("thinking", ReasoningVisibility.Visible, null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContent_WhenVisibilityIsRedacted_TextMayBeNull()
    {
        var content = new ReasoningContent(null, ReasoningVisibility.Redacted, null, ExtensionData.Empty);

        content.Text.ShouldBeNull();
        content.Visibility.ShouldBe(ReasoningVisibility.Redacted);
    }
}
