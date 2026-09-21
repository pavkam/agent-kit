// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Identity;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies facade admission revalidates identity evidence when a validation policy is composed.</summary>
public sealed class AgentIdentityAdmissionTests
{
    [Fact]
    public async Task RunAsync_WhenIdentityEvidenceIsExpired_RejectsBeforeRunOrSessionMutation()
    {
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var loop = new RecordingAgentLoop();
        var runIds = new CountingRunIdGenerator();
        var builder = CompositionTestData.RunnableBuilder(loop);
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.AddSingleton<TimeProvider>(clock);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        _ = builder.Services.AddAgentIdentity(static options => options.MaximumClockSkew = TimeSpan.Zero);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = ExpiredIdentity(now);

        var rejected = (await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            identity,
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();

        rejected.Failure.Code.ShouldBe(AgentErrorCodes.AuthenticationFailed);
        runIds.Created.ShouldBe(0);
        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenIdentityPolicyIsNotComposed_DoesNotRejectExpiredEvidenceAtAdmission()
    {
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var loop = new RecordingAgentLoop();
        var builder = CompositionTestData.RunnableBuilder(loop);
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.AddSingleton<TimeProvider>(clock);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            ExpiredIdentity(now),
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenIdentityEvidenceIsExpired_LogsIdentityRevalidationRejected()
    {
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var logger = new RecordingLogger<AgentEngine>();
        var builder = CompositionTestData.RunnableBuilder(new RecordingAgentLoop());
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.AddSingleton<TimeProvider>(clock);
        _ = builder.Services.AddSingleton<ILogger<AgentEngine>>(_ => logger);
        _ = builder.Services.AddAgentIdentity(static options => options.MaximumClockSkew = TimeSpan.Zero);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = (await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            ExpiredIdentity(now),
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();

        logger.Snapshot().ShouldContain(entry => entry.EventId.Id == 18104);
    }

    private static ExecutionIdentity ExpiredIdentity(DateTimeOffset now) => new(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        ExecutionSubjectKind.Human,
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("evidence"),
            new IdentityIssuerId("issuer"),
            "test",
            now.AddHours(-2),
            now.AddHours(-1),
            new AuthenticationEvidenceFingerprint(new ContentHash("safe"))),
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));
}
