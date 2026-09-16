// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

/// <summary>Verifies RunContinuationCause derived behavior and contracts.</summary>
public sealed class RunContinuationCauseTests
{
    private static readonly OperationId _operationId = new(Guid.Parse("c0000000-0000-0000-0000-000000000001"));

    [Fact]
    public void DeferredCompletionContinuationCause_WhenOperationIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DeferredCompletionContinuationCause(default)).ParamName.ShouldBe("operationId");

    [Fact]
    public void DeferredCompletionContinuationCause_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var cause = new DeferredCompletionContinuationCause(_operationId);
        cause.OperationId.ShouldBe(_operationId);
    }

    [Fact]
    public void DeferredCompletionContinuationCause_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DeferredCompletionContinuationCause(_operationId);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void OutputRepairContinuationCause_WhenDecisionIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new OutputRepairContinuationCause(null!)).ParamName.ShouldBe("decision");

    [Fact]
    public void OutputRepairContinuationCause_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var decision = RetryRequired();
        var cause = new OutputRepairContinuationCause(decision);
        cause.Decision.ShouldBe(decision);
    }

    [Fact]
    public void OutputRepairContinuationCause_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputRepairContinuationCause(RetryRequired());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void CommittedToolResultsContinuationCause_WhenToolResultsAreDefaultOrEmpty_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentException>(() => new CommittedToolResultsContinuationCause(default)).ParamName.ShouldBe("toolResults");
        Should.Throw<ArgumentException>(() => new CommittedToolResultsContinuationCause([])).ParamName.ShouldBe("toolResults");
    }

    [Fact]
    public void CommittedToolResultsContinuationCause_WhenToolResultsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new CommittedToolResultsContinuationCause([null!])).ParamName.ShouldBe("toolResults");

    [Fact]
    public void CommittedToolResultsContinuationCause_WhenToolResultsContainDuplicateCorrelation_ThrowsExactArgumentException()
    {
        var reference = Reference(1);
        Should.Throw<ArgumentException>(() => new CommittedToolResultsContinuationCause([reference, reference])).ParamName.ShouldBe("toolResults");
    }

    [Fact]
    public void CommittedToolResultsContinuationCause_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = Reference(1);
        var cause = new CommittedToolResultsContinuationCause([reference]);
        cause.ToolResults.ShouldBe([reference]);
    }

    [Fact]
    public void CommittedToolResultsContinuationCause_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CommittedToolResultsContinuationCause([Reference(1)]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ExplicitPolicyContinuationCause_WhenReasonCodeIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ExplicitPolicyContinuationCause(" ")).ParamName.ShouldBe("reasonCode");

    [Fact]
    public void ExplicitPolicyContinuationCause_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var cause = new ExplicitPolicyContinuationCause("code");
        cause.ReasonCode.ShouldBe("code");
    }

    [Fact]
    public void ExplicitPolicyContinuationCause_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ExplicitPolicyContinuationCause("code");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void PromotedInputContinuationCause_WhenSnapshotIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new PromotedInputContinuationCause(null!)).ParamName.ShouldBe("snapshot");

    [Fact]
    public void PromotedInputContinuationCause_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var snapshot = Snapshot();
        var cause = new PromotedInputContinuationCause(snapshot);
        cause.Snapshot.ShouldBe(snapshot);
    }

    [Fact]
    public void PromotedInputContinuationCause_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new PromotedInputContinuationCause(Snapshot());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static OutputRetryRequired RetryRequired() =>
        new(new OutputRepairInstruction("fix"), new OutputValidationFailure(OutputValidationFailureKind.SchemaValidationFailed, "invalid", []));

    private static CommittedToolResultReference Reference(int value) =>
        new(new SessionEntryId(Guid.Parse($"c1000000-0000-0000-0000-0000000000{value:D2}")), new ToolCallId(Guid.Parse($"c2000000-0000-0000-0000-0000000000{value:D2}")), new TurnId(Guid.Parse("c3000000-0000-0000-0000-000000000001")));

    private static InputPromotionSnapshot Snapshot()
    {
        var agentId = new AgentId(Guid.Parse("c4000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("c5000000-0000-0000-0000-000000000001"));
        var laneId = new ExecutionLaneId(Guid.Parse("c6000000-0000-0000-0000-000000000001"));
        var operationId = new OperationId(Guid.Parse("c7000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("c8000000-0000-0000-0000-000000000001"));
        var turnId = new TurnId(Guid.Parse("c9000000-0000-0000-0000-000000000001"));
        var branchId = new BranchId(Guid.Parse("ca000000-0000-0000-0000-000000000001"));
        return new InputPromotionSnapshot(agentId, sessionId, laneId, new InRunOperationCorrelation(operationId, runId, turnId),
            new OperationStateRevision(1), new SessionBranchCursor(branchId, null), new SessionSequence(1), null, null,
            PromotionBoundary.BeforeFirstModelRequest, null, turnId, [new AdmissionId(Guid.Parse("cb000000-0000-0000-0000-000000000001"))]);
    }
}
