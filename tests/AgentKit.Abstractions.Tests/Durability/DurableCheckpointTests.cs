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
    public void DurableCheckpoint_Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var address = DurabilityTestData.Address();
        var context = DurabilityTestData.Context();
        var payload = DurabilityTestData.Payload();
        var checkpoint = new DurableCheckpoint(DurabilityTestData.CheckpointId, address, context, DurableCheckpointKind.RunSettled, payload, DurabilityTestData.Token, DurabilityTestData.Now);
        checkpoint.Id.ShouldBe(DurabilityTestData.CheckpointId);
        checkpoint.Address.ShouldBe(address);
        checkpoint.ExecutionContext.ShouldBe(context);
        checkpoint.Kind.ShouldBe(DurableCheckpointKind.RunSettled);
        checkpoint.State.ShouldBe(payload);
        checkpoint.FencingToken.ShouldBe(DurabilityTestData.Token);
        checkpoint.RecordedAt.ShouldBe(DurabilityTestData.Now);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = DurabilityTestData.Checkpoint();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var checkpoint = DurabilityTestData.Checkpoint();
        Should.Throw<ArgumentOutOfRangeException>(() => _ = checkpoint with { Id = default }).ParamName.ShouldBe("Id");
    }

    [Fact]
    public void With_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var checkpoint = DurabilityTestData.Checkpoint();
        Should.Throw<ArgumentOutOfRangeException>(() => _ = checkpoint with { Kind = (DurableCheckpointKind) 99 }).ParamName.ShouldBe("Kind");
    }

    [Fact]
    public void With_WhenStateIsNull_ThrowsArgumentNullException()
    {
        var checkpoint = DurabilityTestData.Checkpoint();
        Should.Throw<ArgumentNullException>(() => _ = checkpoint with { State = null! }).ParamName.ShouldBe("State");
    }

    [Fact]
    public void With_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var checkpoint = DurabilityTestData.Checkpoint();
        Should.Throw<ArgumentOutOfRangeException>(() => _ = checkpoint with { FencingToken = default }).ParamName.ShouldBe("FencingToken");
    }

    [Fact]
    public void With_WhenStateIsValid_UpdatesState()
    {
        var checkpoint = DurabilityTestData.Checkpoint();
        var newPayload = new OperationPayload(new SchemaVersion("v2"), [4, 5, 6]);
        var updated = checkpoint with { State = newPayload };
        updated.State.ShouldBe(newPayload);
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
