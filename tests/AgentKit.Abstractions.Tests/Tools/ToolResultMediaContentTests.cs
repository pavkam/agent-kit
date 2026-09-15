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
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "text/plain", null, [], 0, null, ExtensionData.Empty);
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultMediaContent(reference, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Theory]
    [InlineData(0, typeof(ArgumentOutOfRangeException))]
    [InlineData(1, typeof(ArgumentOutOfRangeException))]
    [InlineData(4, typeof(ArgumentOutOfRangeException))]
    [InlineData(6, typeof(ArgumentException))]
    public void ToolResultMediaContent_Constructor_WhenCopiedReferenceInvalid_ThrowsExactException(int invalidCase, Type exceptionType)
    {
        var valid = Media();
        var malformed = invalidCase switch
        {
            0 => valid with
            {
                Id = default
            },
            1 => valid with
            {
                SourceKind = (MediaSourceKind) 99
            },
            4 => valid with
            {
                SizeInBytes = -1
            },
            6 => valid with
            {
                Uri = new Uri("https://example.test/media")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
        };
        var exception = Should.Throw<ArgumentException>(() => new ToolResultMediaContent(malformed, ExtensionData.Empty));
        exception.GetType().ShouldBe(exceptionType);
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenFileReferenceRetainsUri_PreservesEvidenceWithoutResolution()
    {
        var reference = Media() with
        {
            SourceKind = MediaSourceKind.FileReference,
            Uri = new Uri("file:///workspace/result.txt"),
            InlineBytes = [],
        };
        var content = new ToolResultMediaContent(reference, ExtensionData.Empty);
        content.Reference.ShouldBe(reference);
    }

    private static MediaReference Media() => new(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "text/plain", null, [1], 1, new ContentHash("hash"), ExtensionData.Empty);
}
