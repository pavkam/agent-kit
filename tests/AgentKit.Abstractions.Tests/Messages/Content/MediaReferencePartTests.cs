// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies MediaReferencePart behavior and contracts.</summary>
public sealed class MediaReferencePartTests
{
    [Fact]
    public void MediaReferencePart_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new MediaReferencePart(null!, MediaSemantics.Input, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void MediaReferencePart_Constructor_WhenReferenceNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new MediaReferencePart(null!, MediaSemantics.Input, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void MediaReferencePart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.Uri, "image/png", new Uri("https://example.com/a.png"), [], null, null, ExtensionData.Empty);
        new MediaReferencePart(reference, MediaSemantics.Input, ExtensionData.Empty).ShouldBe(new MediaReferencePart(reference, MediaSemantics.Input, ExtensionData.Empty));
    }

    [Fact]
    public void With_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var part = new MediaReferencePart(
            new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1], 1, null, ExtensionData.Empty),
            MediaSemantics.Input,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Reference = null! });

        exception.ParamName.ShouldBe("value");
    }
}
