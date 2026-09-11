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
        ProviderCredential apiKey = new ApiKeyProviderCredential("k");
        ProviderCredential oauth = new OAuthTokenProviderCredential("t", null);
        _ = apiKey.ShouldBeOfType<ApiKeyProviderCredential>();
        _ = oauth.ShouldBeOfType<OAuthTokenProviderCredential>();
    }
}
