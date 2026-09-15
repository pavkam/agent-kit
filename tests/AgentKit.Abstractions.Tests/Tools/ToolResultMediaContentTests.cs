// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultMediaContent behavior and contracts.</summary>
public sealed class ToolResultMediaContentTests
{
    [Fact]
    public void ToolResultMediaContent_Constructor_WhenReferenceNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultMediaContent(null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "text/plain", null, [1], 1, null, ExtensionData.Empty);
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultMediaContent(reference, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenCopiedReferenceIdIsDefault_ThrowsExactException()
    {
        var malformed = Media() with
        {
            Id = default
        };
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultMediaContent(malformed, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenFileReferenceRetainsUri_PreservesEvidenceWithoutResolution()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.FileReference, "text/plain", new Uri("file:///workspace/result.txt"), [], 1, new ContentHash("hash"), ExtensionData.Empty);
        var content = new ToolResultMediaContent(reference, ExtensionData.Empty);
        content.Reference.ShouldBe(reference);
    }

    private static MediaReference Media() => new(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "text/plain", null, [1], 1, new ContentHash("hash"), ExtensionData.Empty);
}
