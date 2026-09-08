// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies caller-input invariants for admission and promotion evidence.</summary>
public sealed class InputAdmissionContractsTests
{
    [Fact]
    public void ExecutionLaneId_WhenValueIsEmpty_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new ExecutionLaneId(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ExecutionLaneId_WhenValueIsPresent_RetainsTheValidatedValueAndCanonicalText()
    {
        var value = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var lane = new ExecutionLaneId(value);

        lane.Value.ShouldBe(value);
        lane.ToString().ShouldBe("10000000-0000-0000-0000-000000000001");
    }

    [Fact]
    public void OperationStateRevision_WhenValueIsZero_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new OperationStateRevision(0));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void OperationStateRevision_WhenValueIsNegative_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new OperationStateRevision(-1));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void SessionBranchCursor_WhenLastEntryIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new SessionBranchCursor(Branch(), default(SessionEntryId)));
        exception.ParamName.ShouldBe("lastEntryId");
    }

    [Fact]
    public void SessionBranchCursor_WhenBranchIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new SessionBranchCursor(default, null));

        exception.ParamName.ShouldBe("branchId");
    }

    [Fact]
    public void SessionBranchCursor_WhenBranchIsEmpty_RetainsANullTip()
    {
        var cursor = new SessionBranchCursor(Branch(), null);

        cursor.BranchId.ShouldBe(Branch());
        cursor.LastEntryId.ShouldBeNull();
    }

    [Fact]
    public void AgentInput_WhenDeliveryIsUndefined_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new AgentInput(
            Input(1), (InputDelivery) 42, [Part()], ExtensionData.Empty));
        exception.ParamName.ShouldBe("delivery");
    }

    [Fact]
    public void AdmittedInput_WhenEffectivePayloadChangesDelivery_ThrowsArgumentExceptionWithParamName()
    {
        var original = Payload(1, InputDelivery.Steer);
        var effective = Payload(1, InputDelivery.FollowUp);
        var exception = ShouldThrowExactly<ArgumentException>(() => Admitted(original, effective));
        exception.ParamName.ShouldBe("effectivePayload");
    }

    [Fact]
    public void AdmittedInput_WhenPromotionDoesNotFollowAdmission_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => Admitted(payload, payload, new SessionSequence(1)));
        exception.ParamName.ShouldBe("promotedSequence");
    }

    [Fact]
    public void InputPromotionSnapshot_WhenAdmissionsRepeat_ThrowsArgumentExceptionWithParamName()
    {
        var admission = Admission(1);
        var exception = ShouldThrowExactly<ArgumentException>(() => Snapshot([admission, admission]));
        exception.ParamName.ShouldBe("admissionIds");
    }

    [Fact]
    public void InputPromotionContext_WhenEligibleInputTargetsAnotherLane_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var input = new AdmittedInput(Admission(1), Agent(), Session(), OtherLane(), Identity(),
            new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch);
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromotionContext(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [input], 1));
        exception.ParamName.ShouldBe("eligible");
    }

    [Fact]
    public void InputPromotionContext_WhenEligibleInputsAreDuplicatePromotedOrPostCutoff_RejectsBeforePolicyUse()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var pending = Admitted(payload, payload);
        var promoted = Admitted(payload, payload, new SessionSequence(2));
        var duplicateException = ShouldThrowExactly<ArgumentException>(() => PromotionContext([pending, pending]));
        var promotedException = ShouldThrowExactly<ArgumentException>(() => PromotionContext([promoted]));
        var cutoffException = ShouldThrowExactly<ArgumentException>(() => PromotionContext([pending], new SessionSequence(0)));

        duplicateException.ParamName.ShouldBe("eligible");
        promotedException.ParamName.ShouldBe("eligible");
        cutoffException.ParamName.ShouldBe("eligible");
    }

    [Fact]
    public void InputPromoted_WhenRecordsDoNotMatchSnapshotOrder_ThrowsArgumentExceptionWithParamName()
    {
        var first = Admitted(Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), new SessionSequence(3));
        var secondPayload = Payload(2, InputDelivery.Steer);
        var second = new AdmittedInput(Admission(2), Agent(), Session(), Lane(), Identity(), new SessionSequence(1),
            secondPayload, secondPayload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(4));

        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(
            Snapshot([first.AdmissionId, second.AdmissionId]), [second, first], new SessionVersion(1)));

        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void InputPromoted_WhenRecordTargetsAnotherLane_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), OtherLane(), Identity(),
            new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));

        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(
            Snapshot([promoted.AdmissionId]), [promoted], new SessionVersion(1)));

        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void InputPromoted_WhenRecordWasAdmittedAfterSnapshotCutoff_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1),
            payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var snapshot = new InputPromotionSnapshot(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(0), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [Admission(1)]);

        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(snapshot, [promoted], new SessionVersion(1)));

        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void InputPromotionPlanRejected_WhenSelectionLimitDoesNotExceedMaximum_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new InputPromotionPlanRejected(
            InputPromotionPlanRejectionKind.SelectionLimitExceeded, 1, 1, "selection limit"));

        exception.ParamName.ShouldBe("requiredCount");
    }

    [Fact]
    public void AgentInput_WhenPartsAreDefaultOrEmpty_ThrowsArgumentExceptionWithParamName()
    {
        ImmutableArray<ContentPart> uninitialized = default;
        var defaultException = ShouldThrowExactly<ArgumentException>(() => new AgentInput(
            Input(1), InputDelivery.Steer, uninitialized, ExtensionData.Empty));
        var emptyException = ShouldThrowExactly<ArgumentException>(() => new AgentInput(
            Input(1), InputDelivery.Steer, [], ExtensionData.Empty));

        defaultException.ParamName.ShouldBe("parts");
        emptyException.ParamName.ShouldBe("parts");
    }

    [Fact]
    public void AdmissionReceipt_WhenSequenceIsZero_ThrowsArgumentOutOfRangeExceptionBeforeConstruction()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new AdmissionReceipt(
            Admission(1), Input(1), Agent(), Session(), Lane(), new SessionSequence(0), false));

        exception.ParamName.ShouldBe("admittedSequence");
    }

    [Fact]
    public void InputPromotionSnapshot_WhenPreviousTurnDoesNotMatchBoundary_ThrowsArgumentExceptionWithParamName()
    {
        var afterTurnException = ShouldThrowExactly<ArgumentException>(() => new InputPromotionSnapshot(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, null, NextTurn(), []));
        var firstRequestException = ShouldThrowExactly<ArgumentException>(() => new InputPromotionSnapshot(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, PromotionBoundary.BeforeFirstModelRequest, Turn(), NextTurn(), []));

        afterTurnException.ParamName.ShouldBe("previousTurnId");
        firstRequestException.ParamName.ShouldBe("previousTurnId");
    }

    [Fact]
    public void InputPromotionRequest_WhenCommittedBoundaryReusesTargetTurn_ThrowsArgumentExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromotionRequest(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, Identity(), Authorization(Identity(), Agent(), Session(), Operation()),
            PromotionBoundary.AfterTurnCommitted, Turn(), Turn(), 1));

        exception.ParamName.ShouldBe("previousTurnId");
    }

    [Fact]
    public void InputPromotionContext_WhenCheckpointRetainsTargetTurn_PreservesTheValidShape()
    {
        var context = new InputPromotionContext(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, PromotionBoundary.AfterContinuationCheckpoint, Turn(), Turn(), [], 1);

        context.PreviousTurnId.ShouldBe(Turn());
        context.TargetTurnId.ShouldBe(Turn());
    }

    [Fact]
    public void InputAdmissionRequest_WhenAuthorizationDiffersByCorrelation_ThrowsArgumentExceptionWithParamName()
    {
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000001")), null);
        var authorization = Authorization(Identity(), Agent(), Session(), new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000002")), null));

        var exception = ShouldThrowExactly<ArgumentException>(() => new InputAdmissionRequest(
            Agent(), Session(), Lane(), Identity(), correlation, authorization, Payload(1, InputDelivery.Steer)));

        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void InputAdmissionRequest_WhenAuthorizationIdentityOrAddressDiffers_ThrowsArgumentExceptionWithParamName()
    {
        var differentIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var identityException = ShouldThrowExactly<ArgumentException>(() => new InputAdmissionRequest(
            Agent(), Session(), Lane(), Identity(), Operation(), Authorization(differentIdentity, Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)));
        var addressException = ShouldThrowExactly<ArgumentException>(() => new InputAdmissionRequest(
            Agent(), Session(), Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), OtherSession(), Operation()), Payload(1, InputDelivery.Steer)));

        identityException.ParamName.ShouldBe("authorization");
        addressException.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void InputPromotionRequest_WhenInvocationUsesSameRunAndDifferentOperation_IsValid()
    {
        var invocation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000001")), Operation().RunId, Turn());
        var request = PromotionRequest(Authorization(Identity(), Agent(), Session(), invocation));

        request.Authorization.Scope.Correlation.ShouldBe(invocation);
        request.ExpectedOperation.ShouldBe(Operation());
    }

    [Fact]
    public void InputPromotionRequest_WhenAuthorizationUsesAnotherRun_ThrowsArgumentExceptionWithParamName()
    {
        var otherRun = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000001")),
            new RunId(Guid.Parse("90000000-0000-0000-0000-000000000001")), Turn());

        var exception = ShouldThrowExactly<ArgumentException>(() => PromotionRequest(Authorization(Identity(), Agent(), Session(), otherRun)));

        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void InputPromotionRequest_WhenBeforeFirstModelRequestHasPreviousTurn_ThrowsArgumentExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromotionRequest(
            Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, Identity(), Authorization(Identity(), Agent(), Session(), Operation()),
            PromotionBoundary.BeforeFirstModelRequest, Turn(), NextTurn(), 1));

        exception.ParamName.ShouldBe("previousTurnId");
    }

    [Fact]
    public void InputPromotionPlanAndResults_WhenRequiredEvidenceIsNull_ThrowArgumentNullExceptionWithParamName()
    {
        var planException = ShouldThrowExactly<ArgumentNullException>(() => new InputPromotionPlan(null!));
        var rejectedException = ShouldThrowExactly<ArgumentNullException>(() => new InputPromotionRejected(null!));
        var acceptedException = ShouldThrowExactly<ArgumentNullException>(() => new AcceptedInput(null!));

        planException.ParamName.ShouldBe("snapshot");
        rejectedException.ParamName.ShouldBe("rejection");
        acceptedException.ParamName.ShouldBe("receipt");
    }

    [Fact]
    public void InputCapacityAndRejectionEvidence_WhenArgumentsAreInvalid_ThrowsExactExpectedException()
    {
        var capacityException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new InputCapacityLimit(0, 0));
        var retryException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new QueueCapacityExceeded(
            new InputCapacityLimit(1, 0), TimeSpan.FromTicks(-1)));
        var kindException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new InputRejection((InputRejectionKind) 42, "safe"));
        var reasonException = ShouldThrowExactly<ArgumentException>(() => new InputConflict(Input(1), " "));

        capacityException.ParamName.ShouldBe("maximumPendingInputs");
        retryException.ParamName.ShouldBe("retryAfter");
        kindException.ParamName.ShouldBe("kind");
        reasonException.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void InputPromotionGuards_WhenCalledThroughArgumentExceptionType_ValidateExactInputEvidence()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var authorization = Authorization(Identity(), Agent(), Session(), Operation());
        var validEligible = ImmutableArray.Create(Admitted(payload, payload));
        ImmutableArray<AdmissionId> defaultAdmissions = default;
        var otherPayload = Payload(2, InputDelivery.Steer);

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidPromotionEligibleInputs(
            validEligible, Agent(), Session(), Lane(), new SessionSequence(1)));
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidInputAdmissionAuthorization(
            Identity(), Agent(), Session(), Operation(), authorization));
        var admissionsException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfInvalidPromotionAdmissions(defaultAdmissions));
        var payloadException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfInvalidAdmittedInputPayloads(
            payload, otherPayload, Manifest()));

        admissionsException.ParamName.ShouldBe("defaultAdmissions");
        payloadException.ParamName.ShouldBe("otherPayload");
    }

    [Fact]
    public void InputPromotionGuards_WhenCalledDirectly_ValidateBoundaryAndAddressParameters()
    {
        var authorization = Authorization(Identity(), Agent(), Session(), Operation());
        var admissionAddressException = ShouldThrowExactly<ArgumentOutOfRangeException>(() =>
            ArgumentException.ThrowIfInvalidInputAdmissionAuthorization(Identity(), default, Session(), Operation(), authorization));
        var promotionAddressException = ShouldThrowExactly<ArgumentOutOfRangeException>(() =>
            ArgumentException.ThrowIfInvalidInputPromotionAuthorization(Identity(), Agent(), default, Operation(), authorization));
        var operationException = ShouldThrowExactly<ArgumentNullException>(() =>
            ArgumentException.ThrowIfInvalidInputPromotionAuthorization(Identity(), Agent(), Session(), null!, authorization));
        var boundaryException = ShouldThrowExactly<ArgumentOutOfRangeException>(() =>
            ArgumentException.ThrowIfInvalidPromotionTurnBoundary((PromotionBoundary) 42, null, NextTurn()));
        var previousTurnException = ShouldThrowExactly<ArgumentOutOfRangeException>(() =>
            ArgumentException.ThrowIfInvalidPromotionTurnBoundary(PromotionBoundary.AfterContinuationCheckpoint, default(TurnId), NextTurn()));
        var targetTurnException = ShouldThrowExactly<ArgumentOutOfRangeException>(() =>
            ArgumentException.ThrowIfInvalidPromotionTurnBoundary(PromotionBoundary.AfterContinuationCheckpoint, null, default));

        admissionAddressException.ParamName.ShouldBe("agentId");
        promotionAddressException.ParamName.ShouldBe("sessionId");
        operationException.ParamName.ShouldBe("expectedOperation");
        boundaryException.ParamName.ShouldBe("boundary");
        previousTurnException.ParamName.ShouldBe("previousTurnId");
        targetTurnException.ParamName.ShouldBe("targetTurnId");
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void InputConstructors_WhenDocumentedArgumentIsInvalid_ThrowTheExactExceptionBeforeConstruction(
        Func<object?> construct,
        Type exceptionType,
        string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());

        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }

    /// <summary>Supplies one independently invalid public-constructor argument for every uncovered input admission boundary.</summary>
    /// <returns>Constructor delegates paired with their exact documented exception type and parameter name.</returns>
    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        ExtensionData? nullExtensions = null;
        ImmutableArray<ContentPart> nullParts = [null!];
        ExecutionIdentity? nullIdentity = null;
        SecurityAuthorizationContext? nullAuthorization = null;
        OperationCorrelation? nullCorrelation = null;
        AgentInput? nullInput = null;
        AgentInput? nullPayload = null;
        InputPreprocessingManifest? nullManifest = null;

        yield return Case(() => new AgentInput(default, InputDelivery.Steer, [Part()], ExtensionData.Empty), typeof(ArgumentOutOfRangeException), "id");
        yield return Case(() => new AgentInput(Input(1), InputDelivery.Steer, nullParts, ExtensionData.Empty), typeof(ArgumentException), "parts");
        yield return Case(() => new AgentInput(Input(1), InputDelivery.Steer, [Part()], nullExtensions!), typeof(ArgumentNullException), "extensions");

        yield return Case(() => new AdmissionReceipt(default, Input(1), Agent(), Session(), Lane(), new SessionSequence(1), false), typeof(ArgumentOutOfRangeException), "admissionId");
        yield return Case(() => new AdmissionReceipt(Admission(1), default, Agent(), Session(), Lane(), new SessionSequence(1), false), typeof(ArgumentOutOfRangeException), "inputId");
        yield return Case(() => new AdmissionReceipt(Admission(1), Input(1), default, Session(), Lane(), new SessionSequence(1), false), typeof(ArgumentOutOfRangeException), "agentId");
        yield return Case(() => new AdmissionReceipt(Admission(1), Input(1), Agent(), default, Lane(), new SessionSequence(1), false), typeof(ArgumentOutOfRangeException), "sessionId");
        yield return Case(() => new AdmissionReceipt(Admission(1), Input(1), Agent(), Session(), default, new SessionSequence(1), false), typeof(ArgumentOutOfRangeException), "executionLaneId");

        yield return Case(() => new InputPreprocessingManifest(default, new InputFingerprint("original"), new InputFingerprint("effective")), typeof(ArgumentOutOfRangeException), "configurationVersion");
        yield return Case(() => new InputPreprocessingManifest(new ConfigurationVersion(1), default, new InputFingerprint("effective")), typeof(ArgumentOutOfRangeException), "originalFingerprint");
        yield return Case(() => new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint("original"), default), typeof(ArgumentOutOfRangeException), "effectiveFingerprint");

        yield return Case(() => new AdmittedInput(default, Agent(), Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentOutOfRangeException), "admissionId");
        yield return Case(() => new AdmittedInput(Admission(1), default, Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentOutOfRangeException), "agentId");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), default, Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentOutOfRangeException), "sessionId");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), Session(), default, Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentOutOfRangeException), "executionLaneId");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), nullIdentity!, new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentNullException), "identity");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(0), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentOutOfRangeException), "admittedSequence");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), nullPayload!, Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentNullException), "originalPayload");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), nullPayload!, Manifest(), DateTimeOffset.UnixEpoch), typeof(ArgumentNullException), "effectivePayload");
        yield return Case(() => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), nullManifest!, DateTimeOffset.UnixEpoch), typeof(ArgumentNullException), "preprocessing");

        yield return Case(() => new InputAdmissionRequest(default, Session(), Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)), typeof(ArgumentOutOfRangeException), "agentId");
        yield return Case(() => new InputAdmissionRequest(Agent(), default, Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)), typeof(ArgumentOutOfRangeException), "sessionId");
        yield return Case(() => new InputAdmissionRequest(Agent(), Session(), default, Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)), typeof(ArgumentOutOfRangeException), "executionLaneId");
        yield return Case(() => new InputAdmissionRequest(Agent(), Session(), Lane(), nullIdentity!, Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)), typeof(ArgumentNullException), "identity");
        yield return Case(() => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), nullCorrelation!, Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)), typeof(ArgumentNullException), "correlation");
        yield return Case(() => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), Operation(), nullAuthorization!, Payload(1, InputDelivery.Steer)), typeof(ArgumentNullException), "authorization");
        yield return Case(() => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), nullInput!), typeof(ArgumentNullException), "input");

        yield return Case(() => MatrixPromotionContext("agentId"), typeof(ArgumentOutOfRangeException), "agentId");
        yield return Case(() => MatrixPromotionContext("sessionId"), typeof(ArgumentOutOfRangeException), "sessionId");
        yield return Case(() => MatrixPromotionContext("executionLaneId"), typeof(ArgumentOutOfRangeException), "executionLaneId");
        yield return Case(() => MatrixPromotionContext("expectedOperation"), typeof(ArgumentNullException), "expectedOperation");
        yield return Case(() => MatrixPromotionContext("operationStateRevision"), typeof(ArgumentOutOfRangeException), "operationStateRevision");
        yield return Case(() => MatrixPromotionContext("branchCursor"), typeof(ArgumentNullException), "branchCursor");
        yield return Case(() => MatrixPromotionContext("expectedFencingToken"), typeof(ArgumentOutOfRangeException), "expectedFencingToken");
        yield return Case(() => MatrixPromotionContext("boundary"), typeof(ArgumentOutOfRangeException), "boundary");
        yield return Case(() => MatrixPromotionContext("targetTurnId"), typeof(ArgumentOutOfRangeException), "targetTurnId");
        yield return Case(() => MatrixPromotionContext("maximumPromotions"), typeof(ArgumentOutOfRangeException), "maximumPromotions");

        yield return Case(() => MatrixPromotionRequest("agentId"), typeof(ArgumentOutOfRangeException), "agentId");
        yield return Case(() => MatrixPromotionRequest("sessionId"), typeof(ArgumentOutOfRangeException), "sessionId");
        yield return Case(() => MatrixPromotionRequest("executionLaneId"), typeof(ArgumentOutOfRangeException), "executionLaneId");
        yield return Case(() => MatrixPromotionRequest("expectedOperation"), typeof(ArgumentNullException), "expectedOperation");
        yield return Case(() => MatrixPromotionRequest("operationStateRevision"), typeof(ArgumentOutOfRangeException), "operationStateRevision");
        yield return Case(() => MatrixPromotionRequest("branchCursor"), typeof(ArgumentNullException), "branchCursor");
        yield return Case(() => MatrixPromotionRequest("identity"), typeof(ArgumentNullException), "identity");
        yield return Case(() => MatrixPromotionRequest("authorization"), typeof(ArgumentNullException), "authorization");
        yield return Case(() => MatrixPromotionRequest("expectedFencingToken"), typeof(ArgumentOutOfRangeException), "expectedFencingToken");
        yield return Case(() => MatrixPromotionRequest("boundary"), typeof(ArgumentOutOfRangeException), "boundary");
        yield return Case(() => MatrixPromotionRequest("targetTurnId"), typeof(ArgumentOutOfRangeException), "targetTurnId");
        yield return Case(() => MatrixPromotionRequest("maximumPromotions"), typeof(ArgumentOutOfRangeException), "maximumPromotions");

        yield return Case(() => MatrixSnapshot("agentId"), typeof(ArgumentOutOfRangeException), "agentId");
        yield return Case(() => MatrixSnapshot("sessionId"), typeof(ArgumentOutOfRangeException), "sessionId");
        yield return Case(() => MatrixSnapshot("executionLaneId"), typeof(ArgumentOutOfRangeException), "executionLaneId");
        yield return Case(() => MatrixSnapshot("expectedOperation"), typeof(ArgumentNullException), "expectedOperation");
        yield return Case(() => MatrixSnapshot("operationStateRevision"), typeof(ArgumentOutOfRangeException), "operationStateRevision");
        yield return Case(() => MatrixSnapshot("branchCursor"), typeof(ArgumentNullException), "branchCursor");
        yield return Case(() => MatrixSnapshot("expectedFencingToken"), typeof(ArgumentOutOfRangeException), "expectedFencingToken");
        yield return Case(() => MatrixSnapshot("boundary"), typeof(ArgumentOutOfRangeException), "boundary");
        yield return Case(() => MatrixSnapshot("targetTurnId"), typeof(ArgumentOutOfRangeException), "targetTurnId");
    }

    private static AdmittedInput Admitted(AgentInput original, AgentInput effective, SessionSequence? promoted = null) =>
        new(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), original, effective,
            Manifest(), DateTimeOffset.UnixEpoch, promoted);

    private static object?[] Case(Func<object?> construct, Type exceptionType, string parameterName) =>
        [construct, exceptionType, parameterName];

    private static InputPromotionContext MatrixPromotionContext(string parameter)
    {
        var agentId = parameter == "agentId" ? default : Agent();
        var sessionId = parameter == "sessionId" ? default : Session();
        var laneId = parameter == "executionLaneId" ? default : Lane();
        var operation = parameter == "expectedOperation" ? null : Operation();
        var revision = parameter == "operationStateRevision" ? default : new OperationStateRevision(1);
        var cursor = parameter == "branchCursor" ? null : new SessionBranchCursor(Branch(), null);
        var fence = parameter == "expectedFencingToken" ? (FencingToken?) default(FencingToken) : null;
        var boundary = parameter == "boundary" ? (PromotionBoundary) 42 : PromotionBoundary.AfterTurnCommitted;
        var targetTurnId = parameter == "targetTurnId" ? default : NextTurn();
        var maximumPromotions = parameter == "maximumPromotions" ? 0 : 1;
        return new InputPromotionContext(
            agentId, sessionId, laneId, operation!, revision, cursor!, new SessionSequence(1), null, fence,
            boundary, Turn(), targetTurnId, [], maximumPromotions);
    }

    private static InputPromotionRequest MatrixPromotionRequest(string parameter)
    {
        var agentId = parameter == "agentId" ? default : Agent();
        var sessionId = parameter == "sessionId" ? default : Session();
        var laneId = parameter == "executionLaneId" ? default : Lane();
        var operation = parameter == "expectedOperation" ? null : Operation();
        var revision = parameter == "operationStateRevision" ? default : new OperationStateRevision(1);
        var cursor = parameter == "branchCursor" ? null : new SessionBranchCursor(Branch(), null);
        var fence = parameter == "expectedFencingToken" ? (FencingToken?) default(FencingToken) : null;
        var identity = parameter == "identity" ? null : Identity();
        var authorization = parameter == "authorization"
            ? null
            : Authorization(Identity(), Agent(), Session(), Operation());
        var boundary = parameter == "boundary" ? (PromotionBoundary) 42 : PromotionBoundary.AfterTurnCommitted;
        var targetTurnId = parameter == "targetTurnId" ? default : NextTurn();
        var maximumPromotions = parameter == "maximumPromotions" ? 0 : 1;
        return new InputPromotionRequest(
            agentId, sessionId, laneId, operation!, revision, cursor!, new SessionSequence(1), null, fence,
            identity!, authorization!, boundary, Turn(), targetTurnId, maximumPromotions);
    }

    private static InputPromotionSnapshot MatrixSnapshot(string parameter)
    {
        var agentId = parameter == "agentId" ? default : Agent();
        var sessionId = parameter == "sessionId" ? default : Session();
        var laneId = parameter == "executionLaneId" ? default : Lane();
        var operation = parameter == "expectedOperation" ? null : Operation();
        var revision = parameter == "operationStateRevision" ? default : new OperationStateRevision(1);
        var cursor = parameter == "branchCursor" ? null : new SessionBranchCursor(Branch(), null);
        var fence = parameter == "expectedFencingToken" ? (FencingToken?) default(FencingToken) : null;
        var boundary = parameter == "boundary" ? (PromotionBoundary) 42 : PromotionBoundary.AfterTurnCommitted;
        var targetTurnId = parameter == "targetTurnId" ? default : NextTurn();
        return new InputPromotionSnapshot(
            agentId, sessionId, laneId, operation!, revision, cursor!, new SessionSequence(1), null, fence,
            boundary, Turn(), targetTurnId, []);
    }

    private static InputPromotionSnapshot Snapshot(ImmutableArray<AdmissionId> admissions) =>
        new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
            new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), admissions);

    private static AgentInput Payload(long id, InputDelivery delivery) => new(Input(id), delivery, [Part()], ExtensionData.Empty);
    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static InputPreprocessingManifest Manifest() => new(new ConfigurationVersion(1), new InputFingerprint("original:1"), new InputFingerprint("effective:1"));
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static ExecutionLaneId OtherLane() => new(Guid.Parse("40000000-0000-0000-0000-000000000002"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static SessionId OtherSession() => new(Guid.Parse("30000000-0000-0000-0000-000000000002"));
    private static BranchId Branch() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("50000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(
        new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
        new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());

    private static InputPromotionRequest PromotionRequest(SecurityAuthorizationContext authorization) => new(
        Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
        new SessionSequence(1), null, null, Identity(), authorization, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), 1);

    private static InputPromotionContext PromotionContext(
        ImmutableArray<AdmittedInput> eligible,
        SessionSequence? cutoff = null) => new(
        Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null),
        cutoff ?? new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), eligible, 2);

    private static SecurityAuthorizationContext Authorization(
        ExecutionIdentity identity,
        AgentId agentId,
        SessionId sessionId,
        OperationCorrelation correlation) => new(
        new SecurityProfileKey("profile"), new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000001")),
            new SecurityPolicyVersion(1), new ContentHash("safe")),
        new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(0), new ConfigurationVersion(1),
        new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private static TException ShouldThrowExactly<TException>(Action action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(action);
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
