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
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "   ", null, [1], null, null, ExtensionData.Empty));
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
    public void With_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1], 1, null, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => reference with { Extensions = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenSourceKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new MediaReference(new MediaId(Guid.NewGuid()), (MediaSourceKind) 99, "image/png", null, [], null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("sourceKind");
    }

    [Fact]
    public void Constructor_WhenInlineSourceHasUri_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", new Uri("https://example.test/a.png"), [1], null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("uri");
    }

    [Fact]
    public void Constructor_WhenInlineSourceHasNoBytes_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [], null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("inlineBytes");
    }

    [Fact]
    public void Constructor_WhenUriSourceHasNoUri_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.Uri, "image/png", null, [], null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("uri");
    }

    [Theory]
    [InlineData(MediaSourceKind.Uri)]
    [InlineData(MediaSourceKind.FileReference)]
    public void Constructor_WhenReferenceSourceInlinesBytes_ThrowsArgumentException(MediaSourceKind sourceKind)
    {
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), sourceKind, "image/png", new Uri("https://example.test/a.png"), [1], null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("inlineBytes");
    }

    [Fact]
    public void Constructor_WhenSizeIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1], -1, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("sizeInBytes");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    public void Constructor_WhenSizeIsZeroOrPositive_Succeeds(long sizeInBytes)
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.Uri, "image/png", new Uri("https://example.test/a.png"), [], sizeInBytes, null, ExtensionData.Empty);

        reference.SizeInBytes.ShouldBe(sizeInBytes);
    }

    [Fact]
    public void Constructor_WhenFileReferenceOmitsUri_Succeeds()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.FileReference, "text/plain", null, [], null, null, ExtensionData.Empty);

        reference.Uri.ShouldBeNull();
        reference.InlineBytes.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenFileReferenceCarriesLocatorUri_RetainsUriWithoutResolution()
    {
        var uri = new Uri("file:///workspace/result.txt");

        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.FileReference, "text/plain", uri, [], null, null, ExtensionData.Empty);

        reference.Uri.ShouldBe(uri);
    }

    [Fact]
    public void Constructor_WhenInlineSizeDisagreesWithByteCount_ThrowsArgumentException()
    {
        // The constructor did not require sizeInBytes == inlineBytes.Length when sizeInBytes was supplied,
        // allowing self-contradictory evidence for an InlineBytes medium.
        var exception = Should.Throw<ArgumentException>(() => new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], 4, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("sizeInBytes");
    }

    [Fact]
    public void Constructor_WhenInlineSizeMatchesByteCount_Succeeds()
    {
        var reference = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], 3, null, ExtensionData.Empty);

        reference.SizeInBytes.ShouldBe(3);
    }

    [Fact]
    public void MediaReference_Equality_WhenUriDiffersOnlyByFragment_InstancesAreNotEqual()
    {
        // Uri == other.Uri uses Uri.Equals, which ignores the fragment, so two MediaSourceKind.Uri references
        // differing only in #fragment (which some media hosts use for range/page selection) compared equal and
        // deduped into one.
        var id = new MediaId(Guid.NewGuid());
        var first = new MediaReference(id, MediaSourceKind.Uri, "image/png", new Uri("https://example.test/a.png#page=1"), [], null, null, ExtensionData.Empty);
        var second = new MediaReference(id, MediaSourceKind.Uri, "image/png", new Uri("https://example.test/a.png#page=2"), [], null, null, ExtensionData.Empty);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void MediaReference_Equality_WhenUriFragmentMatches_InstancesAreEqual()
    {
        var id = new MediaId(Guid.NewGuid());
        var first = new MediaReference(id, MediaSourceKind.Uri, "image/png", new Uri("https://example.test/a.png#page=1"), [], null, null, ExtensionData.Empty);
        var second = new MediaReference(id, MediaSourceKind.Uri, "image/png", new Uri("https://example.test/a.png#page=1"), [], null, null, ExtensionData.Empty);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
