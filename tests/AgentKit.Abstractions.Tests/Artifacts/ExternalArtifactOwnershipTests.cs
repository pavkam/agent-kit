// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ExternalArtifactOwnership"/> validation.</summary>
public sealed class ExternalArtifactOwnershipTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var uri = new Uri("https://files.example.com/objects/1");

        var ownership = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), uri, true);

        ownership.ResourceId.ShouldBe(new ExternalArtifactResourceId("vendor:1"));
        ownership.CanonicalUri.ShouldBe(uri);
        ownership.AgentKitMayDelete.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenResourceIdIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ExternalArtifactOwnership(default, new Uri("https://files.example.com/1"), false));
        exception.ParamName.ShouldBe("resourceId");
    }

    [Fact]
    public void Constructor_WhenUriIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), null!, false));
        exception.ParamName.ShouldBe("canonicalUri");
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("https://user:secret@files.example.com/1")]
    [InlineData("https://files.example.com/1?X-Signature=abc")]
    [InlineData("https://files.example.com/1#part")]
    public void Constructor_WhenUriIsRelativeOrCarriesCredentials_ThrowsExactParameter(string value)
    {
        var uri = new Uri(value, UriKind.RelativeOrAbsolute);
        var exception = Should.Throw<ArgumentException>(() => new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), uri, false));
        exception.ParamName.ShouldBe("canonicalUri");
    }

    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreEqual()
    {
        var first = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false);
        var second = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
