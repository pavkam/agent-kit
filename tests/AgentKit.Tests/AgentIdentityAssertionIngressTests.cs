// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Identity;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies facade assertion ingress resolves identity before shared admission.</summary>
public sealed class AgentIdentityAssertionIngressTests
{
    [Fact]
    public async Task RunAsync_WhenAssertionResolves_AdmitsTurn()
    {
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var loop = new RecordingAgentLoop();
        var builder = IdentityRunnableBuilder(now, loop);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            ValidAssertion(now),
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenAssertionIsExpired_RejectsBeforeRun()
    {
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var loop = new RecordingAgentLoop();
        var runIds = new CountingRunIdGenerator();
        var builder = IdentityRunnableBuilder(now, loop);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        var rejected = (await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            ExpiredAssertion(now),
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();

        rejected.Failure.Code.ShouldBe(AgentErrorCodes.AuthenticationFailed);
        runIds.Created.ShouldBe(0);
        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenIdentityIsNotComposed_RejectsWithMissingDependency()
    {
        var loop = new RecordingAgentLoop();
        var builder = CompositionTestData.RunnableBuilder(loop);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        var rejected = (await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            ValidAssertion(now),
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();

        rejected.Failure.Code.ShouldBe(AgentErrorCodes.MissingDependency);
        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StreamAsync_WhenAssertionIsExpired_RejectsBeforeSubscription()
    {
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var loop = new RecordingAgentLoop();
        var builder = IdentityRunnableBuilder(now, loop);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = (await agent.StreamAsync<string>(
            CompositionTestData.SessionId,
            ExpiredAssertion(now),
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunStreamRejected<string>>();

        loop.ReceivedRequests.ShouldBeEmpty();
    }

    private static AgentEngineBuilder IdentityRunnableBuilder(DateTimeOffset now, RecordingAgentLoop loop)
    {
        var builder = CompositionTestData.RunnableBuilder(loop);
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        _ = builder.Services.AddAgentIdentity(static options => options.MaximumClockSkew = TimeSpan.Zero);
        _ = builder.Services.AddIdentityIssuer<AssertionIngressIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        return builder;
    }

    private static IdentityAssertion ValidAssertion(DateTimeOffset now) => Assertion(now.AddMinutes(30), now);

    private static IdentityAssertion ExpiredAssertion(DateTimeOffset now) => Assertion(now.AddHours(-1), now.AddHours(-2));

    private static IdentityAssertion Assertion(DateTimeOffset expiresAt, DateTimeOffset authenticatedAt)
    {
        var issuer = new IdentityIssuerId("issuer");
        var evidence = new AuthenticationEvidence(
            new AuthenticationEvidenceId("evidence"),
            issuer,
            "test",
            authenticatedAt,
            expiresAt,
            new AuthenticationEvidenceFingerprint(new ContentHash("safe")));
        return new IdentityAssertion(issuer, "subject", [], evidence);
    }

    private sealed class AssertionIngressIssuer([ServiceKey] IdentityIssuerId issuerId): IIdentityIssuer
    {
        public IdentityIssuerDescriptor Descriptor { get; } = new(issuerId, new IdentityVersion(1));

        public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(
            AuthenticationEvidence evidence,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IdentityValidationResult>(IdentityValidationPassed.Instance);

        public ValueTask<IdentityNormalizationResult> NormalizeAsync(
            IdentityAssertion assertion,
            CancellationToken cancellationToken = default)
        {
            var identity = new ExecutionIdentity(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human,
                assertion.Evidence with { },
                [],
                [],
                IdentityAssuranceLevel.Basic,
                new IdentityVersion(1));
            return ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalized(identity));
        }
    }
}
