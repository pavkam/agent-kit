// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies DelegatingOAuthCredentialSource behavior and contracts.</summary>
public sealed class DelegatingOAuthCredentialSourceTests
{
    [Fact]
    public void Constructor_WhenTokenProviderIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegatingOAuthCredentialSource(null!)).ParamName.ShouldBe("tokenProvider");

    [Fact]
    public async Task GetCredentialAsync_WhenTokenProviderResolves_ReturnsResolvedCredential()
    {
        var credential = new OAuthTokenProviderCredential("token", null);
        var source = new DelegatingOAuthCredentialSource(new FakeTokenProvider(credential));
        var resolved = await source.GetCredentialAsync(new ProviderId("openai"), TestContext.Current.CancellationToken);
        resolved.ShouldBe(credential);
    }

    private sealed class FakeTokenProvider(OAuthTokenProviderCredential credential): IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(credential);
    }
}
