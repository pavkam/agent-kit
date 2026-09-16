// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;

using AgentKit;

using Shouldly;

/// <summary>Verifies WebSearchSecurityBinding behavior and contracts.</summary>
public sealed class WebSearchSecurityBindingTests
{
    [Fact]
    public void Fingerprint_WhenClassifiedQueryChanges_ChangesEvidence()
    {
        var id = new WebSearchRequestId(Guid.NewGuid());
        var provider = new ProviderId("provider");
        var destination = new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example/");
        var first = WebSearchSecurityBinding.Fingerprint(id, provider, destination, "first", [], WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch);
        var second = WebSearchSecurityBinding.Fingerprint(id, provider, destination, "second", [], WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch);
        second.ShouldNotBe(first);
    }

    [Fact]
    public void Fingerprint_WhenDomainsDiffer_ChangesEvidence()
    {
        var id = new WebSearchRequestId(Guid.NewGuid());
        var provider = new ProviderId("provider");
        var destination = new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example/");
        var first = WebSearchSecurityBinding.Fingerprint(id, provider, destination, "query", [new NormalizedHost("example.com")], WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch);
        var second = WebSearchSecurityBinding.Fingerprint(id, provider, destination, "query", [new NormalizedHost("other.example")], WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch);
        second.ShouldNotBe(first);
    }
}
