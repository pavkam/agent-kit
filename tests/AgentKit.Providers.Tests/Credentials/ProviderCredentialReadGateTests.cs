// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Credentials;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Credentials;
using AgentKit.Providers.Egress;
using AgentKit.TestSupport;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies that <see cref="ProviderCredentialReadGate"/> validates and consumes the credential-read grant before a source reads a secret.</summary>
public sealed class ProviderCredentialReadGateTests
{
    private const string Secret = "sk-gate-secret-value";

    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProviderEgressHarness CreateHarness() =>
        ProviderEgressHarness.Create(new HttpClientHandlerStub(), new FakeTimeProvider(Now));

    private static StaticApiKeyCredentialSource CreateSource(ProviderEgressHarness harness) =>
        new(StaticProviderProfileRuntimeSelector.SourceKey, Secret, harness.Gate);

    [Fact]
    public async Task ConsumeAsync_WhenGrantMatchesTheRequest_ConsumesItOnceWithAuditAndReturnsNull()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var refusal = await harness.Gate.ConsumeAsync(source, request, TestContext.Current.CancellationToken);

        refusal.ShouldBeNull();
        var enforcement = harness.Grants.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(ProviderCredentialReadGate.DefaultAudience);
        enforcement.Kind.ShouldBe(SecurityOperationKind.StateRead);
        enforcement.Effect.ShouldBe(SecurityEffect.Observe);
        _ = harness.Audit.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ConsumeAsync_WhenGrantIsReplayed_RefusesAuthorizationTheSecondTime()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        (await harness.Gate.ConsumeAsync(source, request, TestContext.Current.CancellationToken)).ShouldBeNull();

        var refusal = await harness.Gate.ConsumeAsync(source, request, TestContext.Current.CancellationToken);

        var failure = refusal.ShouldNotBeNull().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failure.SafeMessage.ShouldNotContain(Secret);
    }

    [Fact]
    public async Task ConsumeAsync_WhenGrantAudienceDiffers_RefusesBeforeTheStoreIsTouched()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var foreign = request with { CredentialGrant = request.CredentialGrant with { Audience = ProviderEgress.SecurityAudience } };

        var refusal = await harness.Gate.ConsumeAsync(source, foreign, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsumeAsync_WhenGrantIsAProviderEgressGrant_RefusesBecauseEgressCannotReadACredential()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var egressShaped = request with
        {
            CredentialGrant = request.CredentialGrant with
            {
                Audience = ProviderEgress.SecurityAudience,
                Kind = SecurityOperationKind.Network,
                Effect = SecurityEffect.Egress,
            },
        };

        var refusal = await harness.Gate.ConsumeAsync(source, egressShaped, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("attempt")]
    [InlineData("deadline")]
    [InlineData("account")]
    public async Task ConsumeAsync_WhenRequestDiffersFromTheGrantedEvidence_RefusesBeforeTheStoreIsTouched(string changed)
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var altered = changed switch
        {
            "attempt" => request with { Attempt = request.Attempt + 1 },
            "deadline" => request with { Deadline = request.Deadline.AddSeconds(1) },
            _ => request with
            {
                Credential = new ProviderCredentialProfileSnapshot(
                    request.Credential.Reference,
                    request.Credential.ProviderId,
                    request.Credential.ServiceSurface,
                    request.Credential.SourceKey,
                    new ProviderAccountId("another-account"),
                    request.Credential.RefreshSkew,
                    request.Credential.ConfigurationFingerprint,
                    request.Credential.Extensions),
            },
        };

        var refusal = await harness.Gate.ConsumeAsync(source, altered, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsumeAsync_WhenCredentialProfileRevisionDiffers_RefusesBeforeTheStoreIsTouched()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var newer = request with
        {
            Credential = new ProviderCredentialProfileSnapshot(
                new ProviderCredentialProfileReference(request.Credential.Reference.Key, new ProviderCredentialProfileVersion(2)),
                request.Credential.ProviderId,
                request.Credential.ServiceSurface,
                request.Credential.SourceKey,
                request.Credential.AccountId,
                request.Credential.RefreshSkew,
                request.Credential.ConfigurationFingerprint,
                request.Credential.Extensions),
        };

        var refusal = await harness.Gate.ConsumeAsync(source, newer, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsumeAsync_WhenGrantBelongsToAnotherAuthorizationContext_RefusesBeforeTheStoreIsTouched()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var agentId = new AgentId(Guid.Parse("b0000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("b0000000-0000-0000-0000-000000000002"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("b0000000-0000-0000-0000-000000000003")), admissionId: null);
        var otherOperation = new ProtectedSemanticOperationContext(
            agentId,
            sessionId,
            conversationId: null,
            ProviderEgressHarness.Operation.Identity,
            correlation,
            TestSecurityEvidence.Authorization(agentId, sessionId, correlation, ProviderEgressHarness.Operation.Identity));

        var refusal = await harness.Gate.ConsumeAsync(source, request with { Operation = otherOperation }, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsumeAsync_WhenProfileNamesAnotherSource_RefusesInvalidRequestBeforeTheStoreIsTouched()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var other = new StaticApiKeyCredentialSource(new ProviderCredentialSourceKey("other.source"), Secret, harness.Gate);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(other, harness.Authority, Now, TestContext.Current.CancellationToken);

        var refusal = await harness.Gate.ConsumeAsync(source, request, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsumeAsync_WhenGrantStoreFaults_RefusesAuthorizationWithFixedMessageAndKeepsTheCause()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        var fault = new InvalidOperationException("store offline " + Secret);
        harness.Grants.ConsumptionFault = fault;

        var refusal = await harness.Gate.ConsumeAsync(source, request, TestContext.Current.CancellationToken);

        var failure = refusal.ShouldNotBeNull().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failure.SafeMessage.ShouldBe("Credential-read enforcement was unavailable, so the grant was not consumed.");
        failure.DiagnosticCause.ShouldBeSameAs(fault);
    }

    [Fact]
    public async Task ConsumeAsync_WhenRequiredAuditIsUnavailable_RefusesAuthorization()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        harness.Audit.Result = new SecurityAuditUnavailable("sink offline");

        var refusal = await harness.Gate.ConsumeAsync(source, request, TestContext.Current.CancellationToken);

        refusal.ShouldNotBeNull().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
    }

    [Fact]
    public async Task ConsumeAsync_WhenCallerCancelled_ThrowsOperationCanceledException()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await harness.Gate.ConsumeAsync(source, request, cts.Token));
    }

    [Fact]
    public void DefaultAudience_WhenRead_IsDistinctFromEgressAndNetworkAudiences()
    {
        var harness = CreateHarness();

        ProviderCredentialReadGate.DefaultAudience.ShouldNotBe(ProviderEgress.SecurityAudience);
        ProviderCredentialReadGate.DefaultAudience.ShouldNotBe(harness.Resolver.SecurityAudience);
        ProviderCredentialReadGate.DefaultAudience.ShouldNotBe(harness.Transport.SecurityAudience);
    }

    [Fact]
    public async Task ConsumeAsync_WhenArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var missingSource = await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Gate.ConsumeAsync(null!, request));
        var missingRequest = await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Gate.ConsumeAsync(source, null!));
        missingSource.ParamName.ShouldBe("source");
        missingRequest.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenCollaboratorIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var harness = CreateHarness();
        var intents = new CountingIds<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value));
        var audits = new CountingIds<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value));

        Should.Throw<ArgumentNullException>(() => new ProviderCredentialReadGate(null!, harness.Audit, intents, audits, TimeProvider.System)).ParamName.ShouldBe("grantStore");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialReadGate(harness.Grants, null!, intents, audits, TimeProvider.System)).ParamName.ShouldBe("auditDispatcher");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialReadGate(harness.Grants, harness.Audit, null!, audits, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialReadGate(harness.Grants, harness.Audit, intents, null!, TimeProvider.System)).ParamName.ShouldBe("auditRecordIds");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialReadGate(harness.Grants, harness.Audit, intents, audits, null!)).ParamName.ShouldBe("timeProvider");
    }

    private sealed class CountingIds<TId>(Func<Guid, TId> factory): IIdentifierGenerator<TId>
        where TId : struct
    {
        public TId Create() => factory(Guid.NewGuid());
    }

    private sealed class HttpClientHandlerStub: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
