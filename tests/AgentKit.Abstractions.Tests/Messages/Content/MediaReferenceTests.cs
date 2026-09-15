// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies MediaReference behavior and contracts.</summary>
public sealed class MediaReferenceTests
{
    [Fact]
    public void MediaReference_Equality_WhenSameInlineBytes_InstancesAreEqual()
    {
        var first = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], null, null, ExtensionData.Empty);
        var second = new MediaReference(first.Id, MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], null, null, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void MediaReference_Equality_WhenDifferentInlineBytes_InstancesAreNotEqual()
    {
        var id = new MediaId(Guid.NewGuid());
        var first = new MediaReference(id, MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], null, null, ExtensionData.Empty);
        var second = new MediaReference(id, MediaSourceKind.InlineBytes, "image/png", null, [4, 5, 6], null, null, ExtensionData.Empty);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void MediaReference_WhenMediaTypeIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "   ", null, [], null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("mediaType");
    }

    [Fact]
    public void MediaReference_WhenInlineBytesIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, default, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("inlineBytes");
    }

    [Fact]
    public void MediaReference_WhenArgumentsAreValid_ExposesValues()
    {
        var id = new MediaId(Guid.NewGuid());
        var reference = new MediaReference(id, MediaSourceKind.Uri, "image/png", new Uri("https://example.test/image.png"), [], 1024, new ContentHash("abc123"), ExtensionData.Empty);
        reference.Id.ShouldBe(id);
        _ = reference.Uri.ShouldNotBeNull();
        reference.SizeInBytes.ShouldBe(1024);
    }

    [Fact]
    public void With_WhenMediaTypeIsWhitespace_ThrowsArgumentException()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1], 1, null, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => reference with { MediaType = " " });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenInlineBytesIsDefault_ThrowsArgumentException()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1], 1, null, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => reference with { InlineBytes = default });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1], 1, null, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => reference with { Extensions = null! });

        exception.ParamName.ShouldBe("value");
    }
}
