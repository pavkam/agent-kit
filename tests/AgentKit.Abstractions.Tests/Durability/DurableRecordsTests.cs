// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Exercises the constructor guards of the durable context, descriptor,
/// checkpoint, start, result, and evidence records.
/// </summary>
public sealed class DurableRecordsTests
{
    [Fact]
    public void DurableExecutionContext_Constructor_WhenAuthorizationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DurableExecutionContext(
                new DurabilityProfileKey("p"),
                new DurabilityProfileVersion(1),
                new DurableBackendKey("b"),
                new DurableJournalKey("j"),
                new DurableLeaseManagerKey("l"),
                new RecoveryPolicyKey("r"),
                null!));

        exception.ParamName.ShouldBe("authorization");
    }

    [Theory]
    [InlineData("profileKey", typeof(ArgumentNullException))]
    [InlineData("backendKey", typeof(ArgumentNullException))]
    [InlineData("journalKey", typeof(ArgumentNullException))]
    [InlineData("leaseManagerKey", typeof(ArgumentNullException))]
    [InlineData("recoveryPolicyKey", typeof(ArgumentNullException))]
    public void DurableExecutionContext_Constructor_WhenRequiredSelectionIsDefault_ThrowsExactParameter(
        string parameter,
        Type exceptionType)
    {
        var exception = Record.Exception(() => ContextWithInvalidSelection(parameter));

        _ = exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("ProfileKey", typeof(ArgumentNullException))]
    [InlineData("BackendKey", typeof(ArgumentNullException))]
    [InlineData("JournalKey", typeof(ArgumentNullException))]
    [InlineData("LeaseManagerKey", typeof(ArgumentNullException))]
    [InlineData("RecoveryPolicyKey", typeof(ArgumentNullException))]
    public void DurableExecutionContext_With_WhenRequiredSelectionIsDefault_ThrowsExactPropertyParameter(
        string property,
        Type exceptionType)
    {
        var context = DurabilityTestData.Context();
        var exception = Record.Exception(() => ContextWithInvalidCopy(context, property));

        _ = exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(property);
    }

    [Fact]
    public void DurableExecutionContext_Constructor_WhenProfileVersionIsZero_RetainsPublishedRevision()
    {
        var context = new DurableExecutionContext(
            new DurabilityProfileKey("p"),
            new DurabilityProfileVersion(0),
            new DurableBackendKey("b"),
            new DurableJournalKey("j"),
            new DurableLeaseManagerKey("l"),
            new RecoveryPolicyKey("r"),
            DurabilityTestData.Authorization());

        context.ProfileVersion.ShouldBe(new DurabilityProfileVersion(0));
    }

    [Fact]
    public void DurableExecutionContext_With_WhenProfileVersionIsZero_RetainsRevisionWithoutChangingOriginal()
    {
        var original = DurabilityTestData.Context();
        var copy = original with { ProfileVersion = new DurabilityProfileVersion(0) };

        copy.ProfileVersion.ShouldBe(new DurabilityProfileVersion(0));
        original.ProfileVersion.ShouldBe(new DurabilityProfileVersion(1));
    }

    [Fact]
    public void DurableExecutionContext_With_WhenAuthorizationIsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => DurabilityTestData.Context() with { Authorization = null! });

        exception.ParamName.ShouldBe(nameof(DurableExecutionContext.Authorization));
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Descriptor(nullAddress: true));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenInputIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Descriptor(nullInput: true));

        exception.ParamName.ShouldBe("input");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenIdempotencyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Descriptor(idempotency: (IdempotencyClassification) 99));

        exception.ParamName.ShouldBe("idempotency");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenRetryOwnerIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Descriptor(retryOwner: (DurableRetryOwner) 42));

        exception.ParamName.ShouldBe("retryOwner");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenCausalParentIsEmptyIdentity_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Descriptor(causalParentId: default(OperationId)));

        exception.ParamName.ShouldBe("causalParentId");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenExtensionsOmitted_DefaultsToEmpty() =>
        DurabilityTestData.Descriptor().Extensions.ShouldBe(ExtensionData.Empty);

    [Fact]
    public void RecoverableOperationDescriptor_With_WhenExtensionsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => DurabilityTestData.Descriptor() with { Extensions = null! });

        exception.ParamName.ShouldBe(nameof(RecoverableOperationDescriptor.Extensions));
    }

    [Fact]
    public void DurableCheckpoint_Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableCheckpoint(
                default,
                DurabilityTestData.Address(),
                DurabilityTestData.Context(),
                DurableCheckpointKind.RunSettled,
                DurabilityTestData.Payload(),
                DurabilityTestData.Token,
                DurabilityTestData.Now));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void DurableCheckpoint_Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableCheckpoint(
                DurabilityTestData.CheckpointId,
                DurabilityTestData.Address(),
                DurabilityTestData.Context(),
                (DurableCheckpointKind) 77,
                DurabilityTestData.Payload(),
                DurabilityTestData.Token,
                DurabilityTestData.Now));

        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void DurableCheckpoint_Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableCheckpoint(
                DurabilityTestData.CheckpointId,
                DurabilityTestData.Address(),
                DurabilityTestData.Context(),
                DurableCheckpointKind.RunSettled,
                DurabilityTestData.Payload(),
                default,
                DurabilityTestData.Now));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void DurableOperationStart_Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DurableOperationStart(
                null!,
                DurabilityTestData.Payload(),
                DurabilityTestData.Token,
                DurabilityTestData.Now));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Theory]
    [InlineData((int) DurableOperationState.OutcomeReady)]
    [InlineData((int) DurableOperationState.Completed)]
    [InlineData((int) DurableOperationState.Faulted)]
    public void DurableOperationResult_Constructor_WhenStateRetainsCompleteResult_CreatesResult(int rawState)
    {
        var state = (DurableOperationState) rawState;

        var result = Result(state);

        result.State.ShouldBe(state);
    }

    [Theory]
    [InlineData((int) DurableOperationState.Accepted)]
    [InlineData((int) DurableOperationState.EffectPending)]
    [InlineData((int) DurableOperationState.Waiting)]
    [InlineData(int.MaxValue)]
    public void DurableOperationResult_Constructor_WhenStateDoesNotRetainCompleteResult_ThrowsExactParameter(int rawState)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Result((DurableOperationState) rawState));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("state");
    }

    [Theory]
    [InlineData((int) DurableOperationState.OutcomeReady)]
    [InlineData((int) DurableOperationState.Completed)]
    [InlineData((int) DurableOperationState.Faulted)]
    public void DurableOperationResult_With_WhenStateRetainsCompleteResult_CreatesCopyWithoutChangingOriginal(int rawState)
    {
        var original = Result(DurableOperationState.Completed);
        var copy = original with { State = (DurableOperationState) rawState };

        copy.State.ShouldBe((DurableOperationState) rawState);
        original.State.ShouldBe(DurableOperationState.Completed);
    }

    [Theory]
    [InlineData((int) DurableOperationState.Accepted)]
    [InlineData((int) DurableOperationState.EffectPending)]
    [InlineData((int) DurableOperationState.Waiting)]
    [InlineData(int.MaxValue)]
    public void DurableOperationResult_With_WhenStateDoesNotRetainCompleteResult_ThrowsExactPropertyParameter(int rawState)
    {
        var original = DurabilityTestData.Result();
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => original with { State = (DurableOperationState) rawState });

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(nameof(DurableOperationResult.State));
        original.State.ShouldBe(DurableOperationState.Completed);
    }

    [Fact]
    public void DurableOperationResult_Constructor_WhenFailureMessageOmitted_IsNull() =>
        DurabilityTestData.Result().SafeFailureMessage.ShouldBeNull();

    [Fact]
    public void DurableOperationResult_Constructor_WhenStateIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableOperationResult(
                DurabilityTestData.Address(),
                DurabilityTestData.Context(),
                (DurableOperationState) 55,
                SideEffectCertainty.Unknown,
                DurabilityTestData.Payload(),
                DurabilityTestData.Token,
                DurabilityTestData.Now));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("state");
    }

    [Fact]
    public void RecoveryEvidence_Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new RecoveryEvidence(
                DurabilityTestData.Address(),
                DurabilityTestData.Context(),
                DurableOperationState.EffectPending,
                (SideEffectCertainty) 9,
                startDefinitelyAbsent: false,
                terminalResultRecorded: false));

        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Fact]
    public void RecoveryEvidence_Constructor_WhenLastWriterTokenIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new RecoveryEvidence(
                DurabilityTestData.Address(),
                DurabilityTestData.Context(),
                DurableOperationState.EffectPending,
                SideEffectCertainty.Unknown,
                startDefinitelyAbsent: false,
                terminalResultRecorded: false,
                lastWriterToken: default(FencingToken)));

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
    public void ExternalOperationReference_Constructor_WhenHandleIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ExternalOperationReference(new DurableBackendKey("b"), " "));

        exception.ParamName.ShouldBe("handle");
    }

    private static DurableExecutionContext ContextWithInvalidSelection(string parameter) =>
        new(
            parameter == "profileKey" ? default : new DurabilityProfileKey("p"),
            parameter == "profileVersion" ? default : new DurabilityProfileVersion(1),
            parameter == "backendKey" ? default : new DurableBackendKey("b"),
            parameter == "journalKey" ? default : new DurableJournalKey("j"),
            parameter == "leaseManagerKey" ? default : new DurableLeaseManagerKey("l"),
            parameter == "recoveryPolicyKey" ? default : new RecoveryPolicyKey("r"),
            DurabilityTestData.Authorization());

    private static DurableExecutionContext ContextWithInvalidCopy(DurableExecutionContext context, string property) =>
        property switch
        {
            "ProfileKey" => context with { ProfileKey = default },
            "ProfileVersion" => context with { ProfileVersion = default },
            "BackendKey" => context with { BackendKey = default },
            "JournalKey" => context with { JournalKey = default },
            "LeaseManagerKey" => context with { LeaseManagerKey = default },
            "RecoveryPolicyKey" => context with { RecoveryPolicyKey = default },
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

    private static DurableOperationResult Result(DurableOperationState state) =>
        new(
            DurabilityTestData.Address(),
            DurabilityTestData.Context(),
            state,
            SideEffectCertainty.DefinitelyPerformed,
            DurabilityTestData.Payload(),
            DurabilityTestData.Token,
            DurabilityTestData.Now);

    /// <summary>
    /// Builds a descriptor whose arguments are all valid unless a test
    /// deliberately overrides one, so each guard is reached in turn rather
    /// than being masked by an earlier null check.
    /// </summary>
    private static RecoverableOperationDescriptor Descriptor(
        DurableOperationAddress? address = null,
        OperationPayload? input = null,
        DurableRetryOwner retryOwner = DurableRetryOwner.Caller,
        IdempotencyClassification idempotency = IdempotencyClassification.Idempotent,
        OperationId? causalParentId = null,
        bool nullAddress = false,
        bool nullInput = false) =>
        new(
            nullAddress ? null! : address ?? DurabilityTestData.Address(),
            DurabilityTestData.Context(),
            new DurableOperationName("op"),
            new DurableOperationVersion("v1"),
            new IdempotencyKey("k"),
            nullInput ? null! : input ?? DurabilityTestData.Payload(),
            retryOwner,
            DurableTimeoutOwner.Caller,
            CancellationSemantics.LocalWaitOnly,
            SecurityEffect.Execute,
            idempotency,
            DurabilityTestData.Now,
            causalParentId);
}
