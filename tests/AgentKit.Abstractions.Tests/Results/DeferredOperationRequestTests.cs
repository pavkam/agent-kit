// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class DeferredOperationRequestTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("sessionId")]
    [InlineData("runId")]
    [InlineData("operationId")]
    [InlineData("kind")]
    [InlineData("continuationOwner")]
    [InlineData("effects")]
    [InlineData("inputFingerprint")]
    [InlineData("operation")]
    [InlineData("securityDecision")]
    [InlineData("extensions")]
    public void Constructor_WhenDeferredArgumentIsInvalid_RejectsBeforeCapturingState(string parameter)
    {
        var basis = RunResultTestData.Deferred();
        var exception = Should.Throw<ArgumentException>(() => new DeferredOperationRequest(
            parameter == "id" ? default : basis.Id, parameter == "sessionId" ? default : basis.SessionId,
            parameter == "runId" ? default : basis.RunId, parameter == "operationId" ? default : basis.OperationId,
            parameter == "kind" ? (DeferralKind) (-1) : basis.Kind,
            parameter == "continuationOwner" ? (DeferralContinuationOwner) (-1) : basis.ContinuationOwner,
            parameter == "effects" ? (DeferralEffectState) (-1) : basis.Effects,
            parameter == "operation" ? null! : basis.Operation, parameter == "inputFingerprint" ? default : basis.InputFingerprint,
            parameter == "securityDecision" ? null! : basis.SecurityDecision, basis.CreatedAt, basis.ExpiresAt, basis.SessionVersion,
            parameter == "extensions" ? null! : basis.Extensions));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(parameter is "operation" or "securityDecision" or "extensions" ? typeof(ArgumentNullException) : typeof(ArgumentOutOfRangeException));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_WhenExpiryIsNotAfterCreation_RejectsExactDeadline(long ticks)
    {
        var basis = RunResultTestData.Deferred();
        Should.Throw<ArgumentOutOfRangeException>(() => new DeferredOperationRequest(basis.Id, basis.SessionId, basis.RunId, basis.OperationId,
            basis.Kind, basis.ContinuationOwner, basis.Effects, basis.Operation, basis.InputFingerprint, basis.SecurityDecision,
            basis.CreatedAt, basis.CreatedAt.AddTicks(ticks), basis.SessionVersion, basis.Extensions)).ParamName.ShouldBe("expiresAt");
    }

    [Fact]
    public void Constructor_WhenExpiryIsOneTickAfterCreation_RetainsExclusiveDeadlineWithoutClockLookup()
    {
        var basis = RunResultTestData.Deferred();
        var expiry = basis.CreatedAt.AddTicks(1);
        var request = new DeferredOperationRequest(basis.Id, basis.SessionId, basis.RunId, basis.OperationId,
            basis.Kind, basis.ContinuationOwner, basis.Effects, basis.Operation, basis.InputFingerprint, basis.SecurityDecision,
            basis.CreatedAt, expiry, basis.SessionVersion, basis.Extensions);
        request.ExpiresAt.ShouldBe(expiry);
        request.CreatedAt.ShouldBe(basis.CreatedAt);
    }

    [Theory]
    [InlineData(DeferralKind.ApprovalRequired, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.NotStarted, DeferredResumeEvidenceKind.ApprovalResolution)]
    [InlineData(DeferralKind.ApprovalRequired, DeferralContinuationOwner.RuntimeOperation, DeferralEffectState.NotStarted, DeferredResumeEvidenceKind.ApprovalResolution)]
    [InlineData(DeferralKind.OperationDeferred, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.NotStarted, DeferredResumeEvidenceKind.OperationResult)]
    [InlineData(DeferralKind.ResultPending, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.Started, DeferredResumeEvidenceKind.OperationResult)]
    [InlineData(DeferralKind.ResultPending, DeferralContinuationOwner.RuntimeOperation, DeferralEffectState.Started, DeferredResumeEvidenceKind.OperationResult)]
    [InlineData(DeferralKind.ProviderSuspended, DeferralContinuationOwner.RuntimeOperation, DeferralEffectState.Started, DeferredResumeEvidenceKind.ProviderContinuation)]
    public void Constructor_WhenDeferralIsCoherent_PreservesOwnerAndRequiredEvidence(DeferralKind kind, DeferralContinuationOwner owner, DeferralEffectState effects, DeferredResumeEvidenceKind evidence)
    {
        var result = RunResultTestData.Deferred(kind: kind, owner: owner, effects: effects);
        result.ContinuationOwner.ShouldBe(owner); result.Effects.ShouldBe(effects); result.RequiredEvidence.ShouldBe(evidence);
        result.SecurityDecision.ShouldBe(RunResultTestData.Decision()); result.Operation.ShouldBe(RunResultTestData.ProtectedOperation());
        (result with { }).ShouldBe(result);
    }

    [Theory]
    [InlineData(DeferralKind.ApprovalRequired, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.Started, "effects")]
    [InlineData(DeferralKind.OperationDeferred, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.Started, "effects")]
    [InlineData(DeferralKind.ResultPending, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.NotStarted, "effects")]
    [InlineData(DeferralKind.ProviderSuspended, DeferralContinuationOwner.RuntimeOperation, DeferralEffectState.NotStarted, "effects")]
    [InlineData(DeferralKind.OperationDeferred, DeferralContinuationOwner.RuntimeOperation, DeferralEffectState.NotStarted, "continuationOwner")]
    [InlineData(DeferralKind.ProviderSuspended, DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState.Started, "continuationOwner")]
    public void Constructor_WhenDeferralEvidenceConflicts_RejectsExactArgument(DeferralKind kind, DeferralContinuationOwner owner, DeferralEffectState effects, string parameter) =>
        Should.Throw<ArgumentException>(() => RunResultTestData.Deferred(kind: kind, owner: owner, effects: effects)).ParamName.ShouldBe(parameter);
}
