// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Authorization;

/// <summary>
/// Verifies <see cref="DelegatingOAuthCredentialSource"/>, the shared OAuth
/// credential source reused by every OpenAI-compatible provider package.
/// </summary>
public sealed class DelegatingOAuthCredentialSourceTests
{
    [Fact]
    public async Task GetCredentialAsync_WhenAsked_DelegatesToTokenProvider()
    {
        var expected = new OAuthTokenProviderCredential("access-token", DateTimeOffset.UtcNow.AddHours(1));
        var source = new DelegatingOAuthCredentialSource(new StubTokenProvider(expected));

        var credential = await source.GetCredentialAsync(new ProviderId("openai"), TestContext.Current.CancellationToken);

        credential.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task GetCredentialAsync_WhenTokenProviderResolvesNewCredentialEachCall_ReflectsLatestValue()
    {
        var tokens = new Queue<OAuthTokenProviderCredential>(
        [
            new OAuthTokenProviderCredential("token-1", DateTimeOffset.UtcNow.AddMinutes(5)),
            new OAuthTokenProviderCredential("token-2", DateTimeOffset.UtcNow.AddMinutes(5)),
        ]);
        var source = new DelegatingOAuthCredentialSource(new QueueTokenProvider(tokens));

        var first = await source.GetCredentialAsync(new ProviderId("openai"), TestContext.Current.CancellationToken);
        var second = await source.GetCredentialAsync(new ProviderId("openai"), TestContext.Current.CancellationToken);

        first.ShouldBeOfType<OAuthTokenProviderCredential>().AccessToken.ShouldBe("token-1");
        second.ShouldBeOfType<OAuthTokenProviderCredential>().AccessToken.ShouldBe("token-2");
    }

    [Fact]
    public void Constructor_WhenTokenProviderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegatingOAuthCredentialSource(null!));

    private sealed class StubTokenProvider: IOAuthAccessTokenProvider
    {
        private readonly OAuthTokenProviderCredential _credential;

        public StubTokenProvider(OAuthTokenProviderCredential credential) => _credential = credential;

        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_credential);
    }

    private sealed class QueueTokenProvider: IOAuthAccessTokenProvider
    {
        private readonly Queue<OAuthTokenProviderCredential> _tokens;

        public QueueTokenProvider(Queue<OAuthTokenProviderCredential> tokens) => _tokens = tokens;

        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_tokens.Dequeue());
    }
}
