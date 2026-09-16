// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionRunLeaseAcquired behavior and contracts.</summary>
public sealed class SessionRunLeaseAcquiredTests
{
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    [Fact]
    public void SessionRunLeaseAcquired_Equality_WhenSameValues_InstancesAreEqual()
    {
        var lease = new FakeRunLease();
        new SessionRunLeaseAcquired(lease).ShouldBe(new SessionRunLeaseAcquired(lease));
    }

    private sealed class FakeRunLease: ISessionRunLease
    {
        public RunId RunId => new(_runGuid);
        public SessionLeaseId LeaseId => throw new NotImplementedException();
        public TenantId TenantId => throw new NotImplementedException();
        public AgentId AgentId => throw new NotImplementedException();
        public SessionId SessionId => throw new NotImplementedException();
        public ExecutionLaneId ExecutionLaneId => throw new NotImplementedException();
        public OperationId OperationId => throw new NotImplementedException();
        public OperationStateRevision StateRevision => throw new NotImplementedException();
        public FencingToken? Fence => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask ReleaseAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    [Fact]
    public void LeaseOutcomeConstructors_WhenArgumentsAreInvalid_ThrowExactExceptions() => Should.Throw<ArgumentNullException>(() => new SessionRunLeaseAcquired(null!)).ParamName.ShouldBe("lease");

    [Fact]
    public void SessionRunLeaseAcquired_With_WhenApplied_ProducesEqualCopy()
    {
        var lease = new FakeRunLease();
        var original = new SessionRunLeaseAcquired(lease);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionRunLeaseUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionRunLeaseUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionRunLeaseUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunLeaseUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void SessionRunBusy_WhenActiveOperationIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunBusy(default, new RunId(Guid.NewGuid()))).ParamName.ShouldBe("activeOperationId");

    [Fact]
    public void SessionRunBusy_WhenActiveRunIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunBusy(new OperationId(Guid.NewGuid()), default)).ParamName.ShouldBe("activeRunId");

    [Fact]
    public void SessionRunBusy_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunBusy(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
