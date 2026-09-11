// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableOperationBinding behavior and contracts.</summary>
public sealed class DurableOperationBindingTests
{
    [Fact]
    public void Constructor_WhenInRunCoordinatesMatch_RetainsExactBindingAndDerivedContext()
    {
        var address = DurabilityTestData.Address();
        var context = DurabilityTestData.Context();
        var binding = new DurableOperationBinding(address, context);
        binding.Address.ShouldBe(address);
        binding.ExecutionContext.ShouldBe(context);
        binding.ExecutionContext.AuthorizationScope.ShouldBe(context.Authorization.Scope);
        binding.ExecutionContext.AgentDefinitionRevision.ShouldBe(context.Authorization.AgentDefinitionRevision);
        binding.ExecutionContext.ConfigurationVersion.ShouldBe(context.Authorization.ConfigurationVersion);
    }

    [Fact]
    public void Constructor_WhenAfterRunCoordinatesMatchWithNoTurn_AcceptsCausalRunWithoutClaimingActiveRun()
    {
        var address = new DurableOperationAddress(DurabilityTestData.AgentId, DurabilityTestData.SessionId, DurabilityTestData.RunId, DurabilityTestData.OperationId);
        var context = Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, DurabilityTestData.SessionId, new AfterRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId)));
        var binding = new DurableOperationBinding(address, context);
        binding.Address.TurnId.ShouldBeNull();
        _ = binding.ExecutionContext.AuthorizationScope.Correlation.ShouldBeOfType<AfterRunOperationCorrelation>();
    }

    [Fact]
    public void ThrowIfInvalidDurableOperationBinding_WhenCoordinatesMatch_DoesNotThrow()
    {
        var address = DurabilityTestData.Address();
        var context = DurabilityTestData.Context();
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidDurableOperationBinding(address, context));
    }

    [Fact]
    public void ThrowIfInvalidDurableOperationBinding_WhenAgentDiffers_ThrowsExactExceptionWithInferredParameter()
    {
        var address = DurabilityTestData.Address();
        var context = Context(DurabilityTestData.Authorization(new AgentId(Guid.Parse("f1000000-0000-0000-0000-000000000010")), DurabilityTestData.SessionId, new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, DurabilityTestData.TurnId)));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidDurableOperationBinding(address, context));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ThrowIfInvalidDurableOperationBinding_WhenSessionIsAbsent_UsesExplicitParameter()
    {
        var address = DurabilityTestData.Address();
        var context = Context(DurabilityTestData.Authorization(DurabilityTestData.AgentId, null, new InRunOperationCorrelation(DurabilityTestData.OperationId, DurabilityTestData.RunId, DurabilityTestData.TurnId)));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidDurableOperationBinding(address, context, "binding"));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("binding");
    }

    [Theory]
    [InlineData("Operation")]
    [InlineData("Session")]
    [InlineData("Run")]
    [InlineData("Turn")]
    [InlineData("AfterRunRun")]
    [InlineData("AfterRunTurn")]
    [InlineData("BeforeRun")]
    public void ThrowIfInvalidDurableOperationBinding_WhenAddressCannotExactlyRepresentAuthorization_ThrowsForContext(string mismatchName)
    {
        var mismatch = Enum.Parse<BindingMismatch>(mismatchName);
        var address = Address(mismatch);
        var context = Context(mismatch);
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidDurableOperationBinding(address, context));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void ThrowIfInvalidDurableOperationBinding_WhenAddressOrContextIsNull_ThrowsExactOwningParameter()
    {
        var addressException = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidDurableOperationBinding(null!, DurabilityTestData.Context()));
        var contextException = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidDurableOperationBinding(DurabilityTestData.Address(), null!));
        addressException.GetType().ShouldBe(typeof(ArgumentNullException));
        contextException.GetType().ShouldBe(typeof(ArgumentNullException));
        addressException.ParamName.ShouldBe("address");
        contextException.ParamName.ShouldBe("executionContext");
    }

    [Fact]
    public void DurableOperationBinding_Constructor_WhenAddressOrContextIsNull_ThrowsExactOwningParameter()
    {
        var addressException = Should.Throw<ArgumentNullException>(() => new DurableOperationBinding(null!, DurabilityTestData.Context()));
        var contextException = Should.Throw<ArgumentNullException>(() => new DurableOperationBinding(DurabilityTestData.Address(), null!));
        addressException.GetType().ShouldBe(typeof(ArgumentNullException));
        contextException.GetType().ShouldBe(typeof(ArgumentNullException));
        addressException.ParamName.ShouldBe("address");
        contextException.ParamName.ShouldBe("executionContext");
    }

    [Fact]
    public void CarrierBindingConstructorsAndCopies_WhenGivenExactBinding_PreserveItWithoutIndependentCoordinateMutation()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var checkpoint = new DurableCheckpoint(DurabilityTestData.CheckpointId, binding, DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        var result = new DurableOperationResult(binding, DurableOperationState.Completed, SideEffectCertainty.DefinitelyPerformed, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        var evidence = new RecoveryEvidence(binding, DurableOperationState.OutcomeReady, SideEffectCertainty.Unknown, startDefinitelyAbsent: false, terminalResultRecorded: true);
        var descriptor = new RecoverableOperationDescriptor(binding, new DurableOperationName("operation"), new DurableOperationVersion("v1"), new IdempotencyKey("key"), DurabilityTestData.Payload(), DurableRetryOwner.Caller, DurableTimeoutOwner.Caller, CancellationSemantics.LocalWaitOnly, SecurityEffect.Execute, IdempotencyClassification.NonIdempotent, DurabilityTestData.Now);
        var copiedResult = result with
        {
            State = DurableOperationState.Faulted
        };
        checkpoint.Binding.ShouldBe(binding);
        evidence.Binding.ShouldBe(binding);
        descriptor.Binding.ShouldBe(binding);
        copiedResult.Binding.ShouldBe(binding);
        result.State.ShouldBe(DurableOperationState.Completed);
        copiedResult.State.ShouldBe(DurableOperationState.Faulted);
    }

    [Fact]
    public void CanonicalCarrierConstructors_WhenBindingIsNull_ThrowExactOwningParameter()
    {
        var checkpointException = Should.Throw<ArgumentNullException>(() => new DurableCheckpoint(DurabilityTestData.CheckpointId, null!, DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        var resultException = Should.Throw<ArgumentNullException>(() => new DurableOperationResult(null!, DurableOperationState.Completed, SideEffectCertainty.DefinitelyPerformed, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        var evidenceException = Should.Throw<ArgumentNullException>(() => new RecoveryEvidence(null!, DurableOperationState.OutcomeReady, SideEffectCertainty.Unknown, startDefinitelyAbsent: false, terminalResultRecorded: true));
        var descriptorException = Should.Throw<ArgumentNullException>(() => new RecoverableOperationDescriptor(null!, new DurableOperationName("operation"), new DurableOperationVersion("v1"), new IdempotencyKey("key"), DurabilityTestData.Payload(), DurableRetryOwner.Caller, DurableTimeoutOwner.Caller, CancellationSemantics.LocalWaitOnly, SecurityEffect.Execute, IdempotencyClassification.NonIdempotent, DurabilityTestData.Now));
        foreach (var exception in new[]
        {
            checkpointException,
            resultException,
            evidenceException,
            descriptorException
        }

        )
        {
            exception.GetType().ShouldBe(typeof(ArgumentNullException));
            exception.ParamName.ShouldBe("binding");
        }
    }

    [Fact]
    public void ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding_WhenBindingIsNull_ThrowsExactOwningParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding(null!, Checkpoint(new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context()))));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("binding");
    }

    [Fact]
    public void ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding_WhenCheckpointIsNull_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding(new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context()), null));

    [Fact]
    public void ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding_WhenCheckpointDiffers_UsesInferredAndExplicitParameters()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var checkpoint = Checkpoint(new DurableOperationBinding(DurabilityTestData.Address(), new DurableExecutionContext(new DurabilityProfileKey("other-profile"), new DurabilityProfileVersion(1), new DurableBackendKey("backend"), new DurableJournalKey("journal"), new DurableLeaseManagerKey("leases"), new RecoveryPolicyKey("policy"), DurabilityTestData.Authorization())));
        var inferred = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding(binding, checkpoint));
        var explicitName = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding(binding, checkpoint, "checkpointArgument"));
        inferred.GetType().ShouldBe(typeof(ArgumentException));
        inferred.ParamName.ShouldBe("checkpoint");
        explicitName.GetType().ShouldBe(typeof(ArgumentException));
        explicitName.ParamName.ShouldBe("checkpointArgument");
    }

    private static DurableCheckpoint Checkpoint(DurableOperationBinding binding) => new(DurabilityTestData.CheckpointId, binding, DurableCheckpointKind.RunSettled, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
    private static DurableOperationAddress Address(BindingMismatch mismatch) => mismatch switch
    {
        BindingMismatch.Operation => new DurableOperationAddress(DurabilityTestData.AgentId, DurabilityTestData.SessionId, DurabilityTestData.RunId, new OperationId(Guid.Parse("f2000000-0000-0000-0000-000000000011")), DurabilityTestData.TurnId),
        BindingMismatch.Session => DurabilityTestData.Address(),
        BindingMismatch.Run => DurabilityTestData.Address(),
        BindingMismatch.Turn => DurabilityTestData.Address(),
        BindingMismatch.AfterRunRun => DurabilityTestData.Address(),
        BindingMismatch.AfterRunTurn => DurabilityTestData.Address(),
        BindingMismatch.BeforeRun => DurabilityTestData.Address(),
        _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
    };
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
