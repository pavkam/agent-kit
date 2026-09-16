// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>Verifies SessionRunSlotOwner behavior and contracts.</summary>
public sealed class SessionRunSlotOwnerTests
{
    [Fact]
    public void Constructor_WhenAnyIdentityIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var leaseId = new SessionLeaseId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var stateRevision = new OperationStateRevision(1);

        Should.Throw<ArgumentOutOfRangeException>(() =>
                new SessionRunSlotOwner(default, operationId, runId, stateRevision))
            .ParamName.ShouldBe("leaseId");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                new SessionRunSlotOwner(leaseId, default, runId, stateRevision))
            .ParamName.ShouldBe("operationId");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                new SessionRunSlotOwner(leaseId, operationId, default, stateRevision))
            .ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                new SessionRunSlotOwner(leaseId, operationId, runId, default))
            .ParamName.ShouldBe("stateRevision");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RetainsExactValues()
    {
        var leaseId = new SessionLeaseId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var stateRevision = new OperationStateRevision(1);

        var owner = new SessionRunSlotOwner(leaseId, operationId, runId, stateRevision);

        owner.LeaseId.ShouldBe(leaseId);
        owner.OperationId.ShouldBe(operationId);
        owner.RunId.ShouldBe(runId);
        owner.StateRevision.ShouldBe(stateRevision);
    }

    [Fact]
    public void Equals_WhenEveryFieldMatches_AreEqual()
    {
        var leaseId = new SessionLeaseId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var stateRevision = new OperationStateRevision(1);

        var first = new SessionRunSlotOwner(leaseId, operationId, runId, stateRevision);
        var second = new SessionRunSlotOwner(leaseId, operationId, runId, stateRevision);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenCloned_ProducesAnEquivalentInstance()
    {
        var original = new SessionRunSlotOwner(new SessionLeaseId(Guid.NewGuid()), new OperationId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new OperationStateRevision(1));

        var clone = original with { };

        clone.ShouldBe(original);
    }
}
