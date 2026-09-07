// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;

using AgentKit;

using Shouldly;

public sealed class WebSearchContractsTests
{
    [Fact]
    public void ThrowIfInvalidWebResultUri_WhenCredentialFreeHttps_DoesNotThrow()
    {
        var uri = new Uri("https://example.com/page");

        var action = () => ArgumentException.ThrowIfInvalidWebResultUri(uri);

        action.ShouldNotThrow();
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://user:secret@example.com/")]
    [InlineData("relative")]
    public void ThrowIfInvalidWebResultUri_WhenInvalid_InfersParameterName(string value)
    {
        var uri = new Uri(value, UriKind.RelativeOrAbsolute);

        var action = () => ArgumentException.ThrowIfInvalidWebResultUri(uri);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(uri));
    }

    [Fact]
    public void ThrowIfNotNetworkEndpointResource_WhenKindDiffers_InfersParameterName()
    {
        var resource = new ProtectedResource(ProtectedResourceKind.ApplicationState, "state");

        var action = () => ArgumentException.ThrowIfNotNetworkEndpointResource(resource);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(resource));
    }

    [Fact]
    public void Fingerprint_WhenClassifiedQueryChanges_ChangesEvidence()
    {
        var id = new WebSearchRequestId(Guid.NewGuid());
        var provider = new ProviderId("provider");
        var destination = new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example/");

        var first = WebSearchSecurityBinding.Fingerprint(
            id, provider, destination, "first", [], WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch);
        var second = WebSearchSecurityBinding.Fingerprint(
            id, provider, destination, "second", [], WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch);

        second.ShouldNotBe(first);
    }
}
