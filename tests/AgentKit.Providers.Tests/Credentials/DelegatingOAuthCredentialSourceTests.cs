// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Credentials;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Credentials;
using AgentKit.TestSupport;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies that <see cref="DelegatingOAuthCredentialSource"/> calls the token provider only after a validated grant.</summary>
public sealed class DelegatingOAuthCredentialSourceTests
{
    private const string Secret = "oauth-secret-token";

    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProviderEgressHarness CreateHarness() =>
        ProviderEgressHarness.Create(new StubHandler(), new FakeTimeProvider(Now));

    private static DelegatingOAuthCredentialSource CreateSource(ProviderEgressHarness harness, IOAuthAccessTokenProvider provider) =>
        new(new ProviderCredentialSourceKey("oauth"), provider, harness.Gate);

    [Fact]
    public async Task ResolveAsync_WhenGrantIsValid_AsksTheProviderOnceAndReleasesALeaseWithItsToken()
    {
        var harness = CreateHarness();
        var provider = new CountingTokenProvider(new OAuthTokenProviderCredential(Secret, Now.AddHours(1)));

        var probe = await ProviderCredentialProbe.ProbeAsync(CreateSource(harness, provider), harness.Authority, utcNow: Now, cancellationToken: TestContext.Current.CancellationToken);

        provider.Calls.ShouldBe(1);
        probe.Headers["Authorization"].ShouldBe($"Bearer {Secret}");
    }

    [Fact]
    public async Task ResolveAsync_WhenProviderReturnsNewTokensEachCall_ReflectsTheLatestToken()
    {
        var harness = CreateHarness();
        var tokens = new Queue<OAuthTokenProviderCredential>([new("token-1", null), new("token-2", null)]);
        var source = CreateSource(harness, new QueueTokenProvider(tokens));

        var first = await ProviderCredentialProbe.ProbeAsync(source, harness.Authority, cancellationToken: TestContext.Current.CancellationToken);
        var second = await ProviderCredentialProbe.ProbeAsync(source, harness.Authority, cancellationToken: TestContext.Current.CancellationToken);

        first.Headers["Authorization"].ShouldBe("Bearer token-1");
        second.Headers["Authorization"].ShouldBe("Bearer token-2");
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantIsDenied_NeverCallsTheTokenProvider()
    {
        var harness = CreateHarness();
        var provider = new CountingTokenProvider(new OAuthTokenProviderCredential(Secret, null));
        var source = CreateSource(harness, provider);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var mismatched = request with { Attempt = 2 };

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, mismatched, cancellationToken: TestContext.Current.CancellationToken);

        _ = probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>();
        provider.Calls.ShouldBe(0);
        probe.Headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenProviderThrows_ReturnsAnAuthenticationFailureWithFixedMessageAndTheCause()
    {
        var harness = CreateHarness();
        var fault = new InvalidOperationException("identity said " + Secret);
        var source = CreateSource(harness, new ThrowingTokenProvider(fault));
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request, cancellationToken: TestContext.Current.CancellationToken);

        var failure = probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldBe("The OAuth access token could not be resolved.");
        failure.DiagnosticCause.ShouldBeSameAs(fault);
    }

    [Fact]
    public async Task ResolveAsync_WhenProviderReturnsNull_ReturnsAnAuthenticationFailure()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness, new CountingTokenProvider(null!));
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request, cancellationToken: TestContext.Current.CancellationToken);

        probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>().Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
    }

    [Fact]
    public async Task ResolveAsync_WhenProviderCancels_PropagatesTheCancellation()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness, new ThrowingTokenProvider(new OperationCanceledException()));
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await source.ResolveAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ToString_WhenCalled_NamesTheSourceKeyAndNeverATokenProvider()
    {
        var source = CreateSource(CreateHarness(), new CountingTokenProvider(new OAuthTokenProviderCredential(Secret, null)));

        source.ToString().ShouldContain("oauth");
        source.ToString().ShouldNotContain(Secret);
    }

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsTheExactExceptionWithParamName()
    {
        var gate = CreateHarness().Gate;
        var key = new ProviderCredentialSourceKey("oauth");
        var provider = new CountingTokenProvider(new OAuthTokenProviderCredential(Secret, null));

        Should.Throw<ArgumentOutOfRangeException>(() => new DelegatingOAuthCredentialSource(default, provider, gate)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new DelegatingOAuthCredentialSource(key, null!, gate)).ParamName.ShouldBe("tokenProvider");
        Should.Throw<ArgumentNullException>(() => new DelegatingOAuthCredentialSource(key, provider, null!)).ParamName.ShouldBe("gate");
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var source = CreateSource(CreateHarness(), new CountingTokenProvider(new OAuthTokenProviderCredential(Secret, null)));

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await source.ResolveAsync(null!));

        exception.ParamName.ShouldBe("request");
    }

    private sealed class CountingTokenProvider(OAuthTokenProviderCredential? token): IOAuthAccessTokenProvider
    {
        public int Calls { get; private set; }

        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(token!);
        }
    }

    private sealed class QueueTokenProvider(Queue<OAuthTokenProviderCredential> tokens): IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(tokens.Dequeue());
    }

    private sealed class ThrowingTokenProvider(Exception exception): IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class StubHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
