// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit.TestSupport;

/// <summary>Verifies SessionExecutionCapability behavior and contracts.</summary>
public sealed class SessionExecutionCapabilityTests
{
    [Fact]
    public void SessionExecutionCapability_WhenReferenceIsNull_ThrowsExactArgumentNullException()
    {
        var coordinator = new UnsupportedCoordinator();
        var runCoordinator = new UnsupportedRunCoordinator();
        var profile = TestSecurityEvidence.SessionProfile();
        Should.Throw<ArgumentNullException>(() => new SessionExecutionCapability(null!, coordinator, runCoordinator)).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new SessionExecutionCapability(profile, null!, runCoordinator)).ParamName.ShouldBe("coordinator");
        Should.Throw<ArgumentNullException>(() => new SessionExecutionCapability(profile, coordinator, null!)).ParamName.ShouldBe("runCoordinator");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var coordinator = new UnsupportedCoordinator();
        var runCoordinator = new UnsupportedRunCoordinator();
        var profile = TestSecurityEvidence.SessionProfile();
        var capability = new SessionExecutionCapability(profile, coordinator, runCoordinator);
        capability.Profile.ShouldBe(profile);
        capability.Coordinator.ShouldBeSameAs(coordinator);
        capability.RunCoordinator.ShouldBeSameAs(runCoordinator);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var coordinator = new UnsupportedCoordinator();
        var runCoordinator = new UnsupportedRunCoordinator();
        var profile = TestSecurityEvidence.SessionProfile();
        var original = new SessionExecutionCapability(profile, coordinator, runCoordinator);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private sealed class UnsupportedCoordinator: ISessionCoordinator
    {
        public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnsupportedRunCoordinator: ISessionRunCoordinator
    {
        public ValueTask<SessionRunLeaseResult> AcquireAsync(SessionRunLeaseRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
