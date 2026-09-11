// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableCheckpoint behavior and contracts.</summary>
public sealed class DurableCheckpointTests
{
    [Fact]
    public void DurableCheckpoint_Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableCheckpoint(default, DurabilityTestData.Address(), DurabilityTestData.Context(), DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void DurableCheckpoint_Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableCheckpoint(DurabilityTestData.CheckpointId, DurabilityTestData.Address(), DurabilityTestData.Context(), (DurableCheckpointKind) 77, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void DurableCheckpoint_Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableCheckpoint(DurabilityTestData.CheckpointId, DurabilityTestData.Address(), DurabilityTestData.Context(), DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), default, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void DurableCheckpoint_PairConstructor_WhenBindingIsInconsistent_RejectsBeforeCheckpointStateIsAccepted()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableCheckpoint(DurabilityTestData.CheckpointId, DurabilityTestData.Address(), Context(BindingMismatch.Run), DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("executionContext");
    }

    private static DurableExecutionContext Context(BindingMismatch mismatch) => mismatch switch
    {
        BindingMismatch.Operation => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, DurabilityTestData.TurnId))),
        BindingMismatch.Session => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, new SessionId(Guid.Parse("f3000000-0000-0000-0000-000000000012")), new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, DurabilityTestData.TurnId))),
        BindingMismatch.Run => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new InRunOperationCorrelation(DurabilityTestData.OperationId, new RunId(Guid.Parse("f4000000-0000-0000-0000-000000000013")), DurabilityTestData.TurnId))),
        BindingMismatch.Turn => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, new TurnId(Guid.Parse("f5000000-0000-0000-0000-000000000014"))))),
        BindingMismatch.AfterRunRun => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new AfterRunOperationCorrelation(DurabilityTestData.OperationId, new RunId(Guid.Parse("f6000000-0000-0000-0000-000000000015"))))),
        BindingMismatch.AfterRunTurn => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new AfterRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId))),
        BindingMismatch.BeforeRun => Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new BeforeRunOperationCorrelation(DurabilityTestData.OperationId, null))),
        _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
    };
    private static DurableExecutionContext Context(SecurityAuthorizationContext authorization) => new(new DurabilityProfileKey("profile"), new DurabilityProfileVersion(0), new DurableBackendKey("backend"), new DurableJournalKey("journal"), new DurableLeaseManagerKey("leases"), new RecoveryPolicyKey("policy"), authorization);
    private enum BindingMismatch
    {
        Operation,
        Session,
        Run,
        Turn,
        AfterRunRun,
        AfterRunTurn,
        BeforeRun,
    }
}
