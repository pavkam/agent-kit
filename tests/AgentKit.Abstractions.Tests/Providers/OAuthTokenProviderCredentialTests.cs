// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies OAuthTokenProviderCredential behavior and contracts.</summary>
public sealed class OAuthTokenProviderCredentialTests
{
    [Fact]
    public void OAuthTokenProviderCredential_Constructor_WhenTokenInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new OAuthTokenProviderCredential(" ", null));
    [Fact]
    public void OAuthTokenProviderCredential_Equality_WhenSameValues_InstancesAreEqual() => new OAuthTokenProviderCredential("token", null).ShouldBe(new OAuthTokenProviderCredential("token", null));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OAuthTokenProviderCredential("token", null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void OAuthTokenProviderCredential_ToString_WhenCalled_RedactsTheAccessToken()
    {
        var text = new OAuthTokenProviderCredential("super-secret-token", null).ToString();

        text.ShouldNotContain("super-secret-token");
        text.ShouldContain(OAuthTokenProviderCredential.RedactionMarker);
    }
}
