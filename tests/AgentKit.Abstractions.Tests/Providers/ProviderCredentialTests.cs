// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderCredential behavior and contracts.</summary>
public sealed class ProviderCredentialTests
{
    [Fact]
    public void ProviderCredential_Hierarchy_EveryLeafDerivesFromProviderCredential()
    {
        ProviderCredential apiKey = new ApiKeyProviderCredential("secret");
        ProviderCredential oauth = new OAuthTokenProviderCredential("token", null);
        _ = apiKey.ShouldBeOfType<ApiKeyProviderCredential>();
        _ = oauth.ShouldBeOfType<OAuthTokenProviderCredential>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApiKeyProviderCredential("secret");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
