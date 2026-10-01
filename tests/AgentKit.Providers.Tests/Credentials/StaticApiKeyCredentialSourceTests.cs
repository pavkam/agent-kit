// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Credentials;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Credentials;
using AgentKit.TestSupport;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies that <see cref="StaticApiKeyCredentialSource"/> releases its key only under a validated credential-read grant.</summary>
public sealed class StaticApiKeyCredentialSourceTests
{
    private const string Secret = "sk-static-secret-value";

    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProviderEgressHarness CreateHarness() =>
        ProviderEgressHarness.Create(new StubHandler(), new FakeTimeProvider(Now));

    [Fact]
    public async Task ResolveAsync_WhenGrantIsValid_ReleasesALeaseThatAppliesTheKey()
    {
        var harness = CreateHarness();
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, harness.Gate);

        var probe = await ProviderCredentialProbe.ProbeAsync(source, harness.Authority, utcNow: Now, cancellationToken: TestContext.Current.CancellationToken);

        _ = probe.Resolution.ShouldBeOfType<ProviderCredentialResolved>();
        probe.Headers["Authorization"].ShouldBe($"Bearer {Secret}");
        probe.ApplyFailure.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenCalledTwiceWithFreshGrants_ReleasesAFreshLeaseEachTime()
    {
        var harness = CreateHarness();
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, harness.Gate);

        var first = await ProviderCredentialProbe.ProbeAsync(source, harness.Authority, cancellationToken: TestContext.Current.CancellationToken);
        var second = await ProviderCredentialProbe.ProbeAsync(source, harness.Authority, cancellationToken: TestContext.Current.CancellationToken);

        first.Resolution.ShouldNotBeSameAs(second.Resolution);
        first.Grant.Id.ShouldNotBe(second.Grant.Id);
        harness.Grants.Enforcements.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantWasAlreadyConsumed_ReturnsUnavailableAndAppliesNothing()
    {
        var harness = CreateHarness();
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, harness.Gate);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        _ = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request, cancellationToken: TestContext.Current.CancellationToken);

        var replay = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request, cancellationToken: TestContext.Current.CancellationToken);

        var unavailable = replay.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>();
        unavailable.Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        unavailable.Failure.SafeMessage.ShouldNotContain(Secret);
        unavailable.Failure.ToString().ShouldNotContain(Secret);
        replay.Headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantAudienceDiffers_ReturnsUnavailableWithoutConsumingTheGrant()
    {
        var harness = CreateHarness();
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, harness.Gate);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var foreign = request with { CredentialGrant = request.CredentialGrant with { Audience = new ComponentId("other.audience") } };

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, foreign, cancellationToken: TestContext.Current.CancellationToken);

        _ = probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>();
        harness.Grants.Enforcements.ShouldBeEmpty();
        probe.Headers.ShouldBeEmpty();
    }

    [Fact]
    public void ToString_WhenCalled_NamesTheSourceKeyAndNeverTheKey()
    {
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, CreateHarness().Gate);

        source.ToString().ShouldContain("static");
        source.ToString().ShouldNotContain(Secret);
    }

    [Fact]
    public void SecurityAudience_WhenRead_IsTheSharedCredentialAudience()
    {
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, CreateHarness().Gate);

        source.SecurityAudience.ShouldBe(ProviderCredentialReadGate.DefaultAudience);
        source.Key.ShouldBe(new ProviderCredentialSourceKey("static"));
    }

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsTheExactExceptionWithParamName()
    {
        var gate = CreateHarness().Gate;
        var key = new ProviderCredentialSourceKey("static");

        Should.Throw<ArgumentOutOfRangeException>(() => new StaticApiKeyCredentialSource(default, Secret, gate)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => new StaticApiKeyCredentialSource(key, " ", gate)).ParamName.ShouldBe("apiKey");
        Should.Throw<ArgumentException>(() => new StaticApiKeyCredentialSource(key, null!, gate)).ParamName.ShouldBe("apiKey");
        Should.Throw<ArgumentNullException>(() => new StaticApiKeyCredentialSource(key, Secret, null!)).ParamName.ShouldBe("gate");
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var source = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("static"), Secret, CreateHarness().Gate);

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await source.ResolveAsync(null!));

        exception.ParamName.ShouldBe("request");
    }

    private sealed class StubHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
