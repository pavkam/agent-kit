// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;
using AgentKit.TestSupport;

/// <summary>Verifies RecoveryEvidence behavior and contracts.</summary>
public sealed class RecoveryEvidenceTests
{
    [Fact]
    public void RecoveryEvidence_Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RecoveryEvidence(DurabilityTestData.Address(), DurabilityTestData.Context(), DurableOperationState.EffectPending, (SideEffectCertainty) 9, startDefinitelyAbsent: false, terminalResultRecorded: false));
        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RecoveryEvidence_Constructor_WhenCertaintyIsCanonicalNumericValue_RetainsValue(int rawCertainty)
    {
        var certainty = (SideEffectCertainty) rawCertainty;
        var evidence = new RecoveryEvidence(DurabilityTestData.Address(), DurabilityTestData.Context(), DurableOperationState.EffectPending, certainty, startDefinitelyAbsent: false, terminalResultRecorded: false);
        evidence.SideEffectCertainty.ShouldBe(certainty);
    }

    [Fact]
    public void RecoveryEvidence_Constructor_WhenLastWriterTokenIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RecoveryEvidence(DurabilityTestData.Address(), DurabilityTestData.Context(), DurableOperationState.EffectPending, SideEffectCertainty.Unknown, startDefinitelyAbsent: false, terminalResultRecorded: false, lastWriterToken: default(FencingToken)));
        exception.ParamName.ShouldBe("lastWriterToken");
    }

    [Fact]
    public void RecoveryEvidence_Constructor_WhenOptionalEvidenceOmitted_IsNull()
    {
        var evidence = DurabilityTestData.Evidence();
        evidence.LatestCheckpoint.ShouldBeNull();
        evidence.ExternalReference.ShouldBeNull();
        evidence.ExternalIdempotencyKey.ShouldBeNull();
        evidence.LastWriterToken.ShouldBeNull();
    }

    [Fact]
    public void RecoveryEvidence_WhenCheckpointBindingHasDifferentAddress_RejectsEvidenceConstruction()
    {
        var evidenceBinding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var checkpointBinding = new DurableOperationBinding(new DurableOperationAddress(new AgentId(Guid.Parse("f7000000-0000-0000-0000-000000000016")), DurabilityTestData.SessionId, DurabilityTestData.RunId, DurabilityTestData.OperationId, DurabilityTestData.TurnId), DurabilityTestData.Context(DurabilityTestData.Authorization(new AgentId(Guid.Parse("f7000000-0000-0000-0000-000000000016")), DurabilityTestData.SessionId, new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, DurabilityTestData.TurnId))));
        var exception = Should.Throw<ArgumentException>(() => Evidence(evidenceBinding, Checkpoint(checkpointBinding)));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("latestCheckpoint");
    }

    [Fact]
    public void RecoveryEvidence_WhenCheckpointBindingHasDifferentDurabilityContext_RejectsEvidenceConstruction()
    {
        var authorization = DurabilityTestData.Authorization();
        var evidenceBinding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context(authorization));
        var checkpointBinding = new DurableOperationBinding(DurabilityTestData.Address(), new DurableExecutionContext(new DurabilityProfileKey("other-profile"), new DurabilityProfileVersion(0), new DurableBackendKey("backend"), new DurableJournalKey("journal"), new DurableLeaseManagerKey("leases"), new RecoveryPolicyKey("policy"), authorization));
        var exception = Should.Throw<ArgumentException>(() => Evidence(evidenceBinding, Checkpoint(checkpointBinding)));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("latestCheckpoint");
    }

    [Fact]
    public void RecoveryEvidence_WhenCheckpointBindingHasDifferentTenantIdentity_RejectsConstructionAndCopyWithoutChangingOriginal()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var originalCheckpoint = Checkpoint(binding);
        var original = Evidence(binding, originalCheckpoint);
        var otherTenantAuthorization = DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, DurabilityTestData.TurnId), TestExecutionIdentity.Create(new TenantId("other-tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
        var otherTenantBinding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context(otherTenantAuthorization));
        var otherTenantCheckpoint = Checkpoint(otherTenantBinding);
        var constructionException = Should.Throw<ArgumentException>(() => Evidence(binding, otherTenantCheckpoint));
        var copyException = Should.Throw<ArgumentException>(() => original with { LatestCheckpoint = otherTenantCheckpoint });
        constructionException.GetType().ShouldBe(typeof(ArgumentException));
        constructionException.ParamName.ShouldBe("latestCheckpoint");
        copyException.GetType().ShouldBe(typeof(ArgumentException));
        copyException.ParamName.ShouldBe(nameof(RecoveryEvidence.LatestCheckpoint));
        original.LatestCheckpoint.ShouldBe(originalCheckpoint);
    }

    [Fact]
    public void RecoveryEvidence_WhenCheckpointBindingIsReconstructedEqual_AcceptsExactEvidence()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var reconstructedBinding = new DurableOperationBinding(new DurableOperationAddress(DurabilityTestData.AgentId, DurabilityTestData.SessionId, DurabilityTestData.RunId, DurabilityTestData.OperationId, DurabilityTestData.TurnId), DurabilityTestData.Context(DurabilityTestData.Authorization()));
        var checkpoint = Checkpoint(reconstructedBinding);
        ArgumentException.ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding(binding, checkpoint);
        var evidence = Evidence(binding, checkpoint);
        reconstructedBinding.ShouldBe(binding);
        evidence.LatestCheckpoint.ShouldBe(checkpoint);
    }

    private static RecoveryEvidence Evidence(DurableOperationBinding binding, DurableCheckpoint? latestCheckpoint = null) => new(binding, DurableOperationState.OutcomeReady, SideEffectCertainty.Unknown, startDefinitelyAbsent: false, terminalResultRecorded: true, latestCheckpoint);
    private static DurableCheckpoint Checkpoint(DurableOperationBinding binding) => new(DurabilityTestData.CheckpointId, binding, DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);

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
