// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Shared process executor contract scenarios for real and scripted adapters.</summary>
/// <typeparam name="TFixture">The adapter-specific fixture.</typeparam>
public abstract class ProcessExecutorConformanceTests<TFixture>
    where TFixture : IProcessExecutorConformanceFixture
{
    /// <summary>Creates an isolated fixture for one inherited case.</summary>
    /// <returns>The fixture instance.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies a valid grant returns a handle that streams declared output and exits.</summary>
    [Fact]
    public async Task StartAsync_WhenGrantIsAuthorized_ReturnsHandleWithOutputAndExit()
    {
        await using var fixture = CreateFixture();
        var request = fixture.CreateStartRequest();
        var resolution = await fixture.Resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        var resolved = resolution.ShouldBeOfType<ExecutableResolved>().Resolved;
        var grant = await fixture.CreateAuthorizedGrantAsync(request);
        var start = await fixture.Executor.StartAsync(resolved, grant, TestContext.Current.CancellationToken);
        var handle = start.ShouldBeOfType<ProcessHandleStarted>().Handle;
        await using var owned = handle;
        var stdout = new List<byte>();
        await foreach (var evt in handle.ReadOutputAsync(TestContext.Current.CancellationToken))
        {
            if (evt is ProcessStandardOutputBytes bytes)
            {
                stdout.AddRange(bytes.Bytes.ToArray());
            }
        }

        var exit = await handle.Completion;
        _ = exit.ShouldBeOfType<ProcessExited>();
        stdout.ShouldNotBeEmpty();
    }

    /// <summary>Verifies a mismatched grant is denied before a handle is returned.</summary>
    [Fact]
    public async Task StartAsync_WhenGrantIsNotRegistered_ReturnsDenied()
    {
        await using var fixture = CreateFixture();
        var request = fixture.CreateStartRequest();
        var resolution = await fixture.Resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        var resolved = resolution.ShouldBeOfType<ExecutableResolved>().Resolved;
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
            new SecurityRequestId(Guid.Parse("91000000-0000-0000-0000-000000000010")),
            new SecurityAuthorizationScope(new AgentId(Guid.Parse("92000000-0000-0000-0000-000000000011")), null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("93000000-0000-0000-0000-000000000012")), null)),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            fixture.Executor.SecurityAudience,
            SecurityOperationKind.Process,
            SecurityEffect.Execute,
            [new ProtectedResource(ProtectedResourceKind.Process, "/denied")],
            new InputFingerprint("sha256:denied"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(5),
            1);
        var start = await fixture.Executor.StartAsync(resolved, grant, TestContext.Current.CancellationToken);
        _ = start.ShouldBeOfType<ProcessStartDenied>();
    }

    /// <summary>Verifies drift between captured and fresh resolution closes before start.</summary>
    [Fact]
    public async Task StartAsync_WhenResolvedFactsDrift_ReturnsResolutionFailed()
    {
        await using var fixture = CreateFixture();
        if (fixture is not ISupportsProcessResolutionDriftFixture drift)
        {
            return;
        }

        var request = fixture.CreateStartRequest();
        var resolution = await fixture.Resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        var resolved = resolution.ShouldBeOfType<ExecutableResolved>().Resolved;
        drift.EnableResolutionDrift();
        var grant = await fixture.CreateAuthorizedGrantAsync(request);
        var start = await fixture.Executor.StartAsync(resolved, grant, TestContext.Current.CancellationToken);
        _ = start.ShouldBeOfType<ProcessStartResolutionFailed>();
    }
}

/// <summary>Optional capability marker for resolution drift scenarios.</summary>
public interface ISupportsProcessResolutionDriftFixture
{
    /// <summary>Causes the next executor revalidation to observe different canonical facts.</summary>
    public void EnableResolutionDrift();
}
