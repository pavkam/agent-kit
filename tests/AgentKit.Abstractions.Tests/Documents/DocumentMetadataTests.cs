// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

/// <summary>Verifies <see cref="DocumentMetadata"/> constraints.</summary>
public sealed class DocumentMetadataTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var metadata = new DocumentMetadata("Title", "text/plain", null, ExtensionData.Empty);

        metadata.Title.ShouldBe("Title");
        metadata.MediaType.ShouldBe("text/plain");
        metadata.SourceArtifact.ShouldBeNull();
        metadata.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Constructor_WhenTitleIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentMetadata(" ", "text/plain", null, ExtensionData.Empty)).ParamName.ShouldBe("title");

    [Fact]
    public void Constructor_WhenTitleIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentMetadata(null!, "text/plain", null, ExtensionData.Empty)).ParamName.ShouldBe("title");

    [Fact]
    public void Constructor_WhenMediaTypeIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentMetadata("t", "", null, ExtensionData.Empty)).ParamName.ShouldBe("mediaType");

    [Fact]
    public void Constructor_WhenExtensionsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentMetadata("t", "text/plain", null, null!)).ParamName.ShouldBe("extensions");
}
