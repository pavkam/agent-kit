// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class AgentRunFinishedTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenSettlementDiffers_PreservesIdenticalSemanticOutputAndUsage(bool completed)
    {
        var message = RunResultTestData.Message();
        RunSettlementOutcome settlement = completed ? new RunSettlementCompleted() : new RunSettlementRecoveryRequired(RunResultTestData.Error(AgentErrorCodes.StoreUnavailable));
        var result = RunResultTestData.Finished(settlement: settlement, messages: [message]);
        _ = result.Outcome.ShouldBeOfType<RunSucceeded>();
        result.Output.ShouldBe("output");
        result.NewMessages.ShouldHaveSingleItem().ShouldBeSameAs(message);
        result.Settlement.ShouldBeSameAs(settlement);
        result.Usage.RunId.ShouldBe(RunResultTestData.Run);
        result.IsCleanSuccess.ShouldBe(completed);
        (result with { }).ShouldBe(result);
    }

    [Fact]
    public void Constructor_WhenExternalHandoffCompletes_RetainsCorrelatedRequestsInBothOutcomeAndEnvelope()
    {
        var first = RunResultTestData.Deferred();
        var second = RunResultTestData.Deferred(2);
        var deferred = new RunDeferred([first, second]);
        var result = RunResultTestData.Finished(deferred, deferred: [first, second]);
        result.DeferredRequests.ShouldBe([first, second]);
        result.IsCleanSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenRuntimeOwnsSuspension_RejectsTerminalHandoffAndFinishedEnvelope()
    {
        var suspended = RunResultTestData.Deferred(kind: DeferralKind.ProviderSuspended,
            owner: DeferralContinuationOwner.RuntimeOperation, effects: DeferralEffectState.Started);
        Should.Throw<ArgumentException>(() => RunResultTestData.Finished(deferred: [suspended])).ParamName.ShouldBe("deferredRequests");
        suspended.RequiredEvidence.ShouldBe(DeferredResumeEvidenceKind.ProviderContinuation);
    }

    [Fact]
    public void Constructor_WhenHandoffEvidenceDiffers_RejectsMissingAndReorderedRequests()
    {
        var first = RunResultTestData.Deferred(); var second = RunResultTestData.Deferred(2);
        var outcome = new RunDeferred([first, second]);
        Should.Throw<ArgumentException>(() => RunResultTestData.Finished(outcome, deferred: [first])).ParamName.ShouldBe("deferredRequests");
        Should.Throw<ArgumentException>(() => RunResultTestData.Finished(outcome, deferred: [second, first])).ParamName.ShouldBe("deferredRequests");
    }

    [Theory]
    [InlineData(MessageState.Interrupted)]
    [InlineData(MessageState.Suspended)]
    public void Constructor_WhenCommittedPartialEvidenceExists_PreservesItsStateAndRole(MessageState state)
    {
        var message = RunResultTestData.Message() with { State = state };
        var result = RunResultTestData.Finished(new RunCancelled(new(RunResultTestData.Error(AgentErrorCodes.Cancelled))), messages: [message]);
        result.NewMessages[0].ShouldBeOfType<UserMessage>().State.ShouldBe(state);
        result.NewMessages[0].ShouldBeSameAs(message);
        result.IsCleanSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenEvidenceIsIndependentlyReconstructed_UsesStructuralEqualityAndHashes()
    {
        var first = RunResultTestData.Finished(messages: [RunResultTestData.Message()], deferred: [RunResultTestData.Deferred()]);
        var second = RunResultTestData.Finished(messages: [RunResultTestData.Message()], deferred: [RunResultTestData.Deferred()]);
        first.ShouldBe(second); first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(RunResultTestData.Finished(new RunIdle()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenCommittedInstructionsHaveNoRun_PreservesEstablishedRole(bool system)
    {
        var basis = RunResultTestData.Message();
        AgentMessage message = system
            ? new SystemMessage(basis.Id, basis.AgentId, basis.SessionId, null, basis.BranchId, null, null, basis.CreatedAt, basis.State, basis.Parts, basis.Extensions)
            : new DeveloperMessage(basis.Id, basis.AgentId, basis.SessionId, null, basis.BranchId, null, null, basis.CreatedAt, basis.State, basis.Parts, basis.Extensions);
        var result = RunResultTestData.Finished(messages: [message]);
        result.NewMessages.ShouldHaveSingleItem().ShouldBeSameAs(message);
        result.NewMessages[0].RunId.ShouldBeNull();
    }

    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("conversationId")]
    [InlineData("runId")]
    public void Constructor_WhenFinishedIdentityIsDefault_RejectsExactArgument(string parameter)
    {
        var result = RunResultTestData.Finished();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunFinished<string>(
            parameter == "agentId" ? default : result.AgentId, parameter == "sessionId" ? default : result.SessionId,
            parameter == "conversationId" ? default(ConversationId) : null, parameter == "runId" ? default : result.RunId,
            result.Outcome, result.Settlement, result.Output, result.PreviousCursor, [], result.Usage, [], result.Metadata));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("outcome")]
    [InlineData("settlement")]
    [InlineData("previousCursor")]
    [InlineData("usage")]
    [InlineData("metadata")]
    public void Constructor_WhenFinishedEvidenceIsNull_RejectsExactArgument(string parameter)
    {
        var result = RunResultTestData.Finished();
        var exception = Should.Throw<ArgumentNullException>(() => new AgentRunFinished<string>(result.AgentId, result.SessionId, null, result.RunId,
            parameter == "outcome" ? null! : result.Outcome, parameter == "settlement" ? null! : result.Settlement, result.Output,
            parameter == "previousCursor" ? null! : result.PreviousCursor, [], parameter == "usage" ? null! : result.Usage, [], parameter == "metadata" ? null! : result.Metadata));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("session")]
    [InlineData("conversation")]
    [InlineData("usage")]
    public void Constructor_WhenCursorOrUsageAddressesDifferentRun_RejectsExactEvidence(string mismatch)
    {
        var result = RunResultTestData.Finished();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var cursor = new MessageCursor(mismatch == "agent" ? new AgentId(id) : result.AgentId,
            mismatch == "session" ? new SessionId(id) : result.SessionId, mismatch == "conversation" ? new ConversationId(id) : null,
            result.PreviousCursor.BranchId, default, default);
        var exception = Should.Throw<ArgumentException>(() => new AgentRunFinished<string>(result.AgentId, result.SessionId, null, result.RunId,
            result.Outcome, result.Settlement, result.Output, cursor, [], mismatch == "usage" ? new RunUsage(new RunId(id), []) : result.Usage, [], result.Metadata));
        exception.ParamName.ShouldBe(mismatch == "usage" ? "usage" : "previousCursor");
    }

    [Theory]
    [InlineData("default", false)]
    [InlineData("null", true)]
    [InlineData("duplicate", false)]
    public void Constructor_WhenNewMessageCollectionIsInvalid_RejectsExactArgument(string invalid, bool nullElement)
    {
        var basis = RunResultTestData.Finished(); var message = RunResultTestData.Message();
        ImmutableArray<AgentMessage> messages = invalid == "default" ? default : invalid == "null" ? [null!] : [message, message];
        var exception = Should.Throw<ArgumentException>(() => new AgentRunFinished<string>(basis.AgentId, basis.SessionId, null, basis.RunId,
            basis.Outcome, basis.Settlement, basis.Output, basis.PreviousCursor, messages, basis.Usage, [], basis.Metadata));
        exception.ParamName.ShouldBe("newMessages");
        exception.GetType().ShouldBe(nullElement ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Theory]
    [InlineData("id", "range")]
    [InlineData("agent", "argument")]
    [InlineData("session", "argument")]
    [InlineData("conversation", "argument")]
    [InlineData("branch", "argument")]
    [InlineData("run", "argument")]
    [InlineData("null-run", "argument")]
    [InlineData("default-run", "range")]
    [InlineData("turn", "range")]
    [InlineData("state", "range")]
    [InlineData("incomplete", "argument")]
    public void Constructor_WhenRetainedMessageIsInvalid_RejectsBeforeCapturingEvidence(string invalid, string exceptionKind)
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var basis = RunResultTestData.Message();
        var message = invalid switch
        {
            "id" => basis with { Id = default },
            "agent" => basis with { AgentId = new AgentId(id) },
            "session" => basis with { SessionId = new SessionId(id) },
            "conversation" => basis with { ConversationId = new ConversationId(id) },
            "branch" => basis with { BranchId = new BranchId(id) },
            "run" => basis with { RunId = new RunId(id) },
            "null-run" => basis with { RunId = null },
            "default-run" => basis with { RunId = default(RunId) },
            "turn" => basis with { TurnId = default(TurnId) },
            "state" => basis with { State = (MessageState) (-1) },
            _ => basis with { State = MessageState.Incomplete },
        };
        var exception = Should.Throw<ArgumentException>(() => RunResultTestData.Finished(messages: [message]));
        exception.ParamName.ShouldBe("newMessages");
        exception.GetType().ShouldBe(exceptionKind == "range" ? typeof(ArgumentOutOfRangeException) : exceptionKind == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void Constructor_WhenOutcomeIsLegacy_RequiresExplicitCanonicalMapping() =>
        Should.Throw<ArgumentException>(() => RunResultTestData.Finished(new AgentRunIdle())).ParamName.ShouldBe("outcome");

    [Theory]
    [InlineData("default")]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("session")]
    [InlineData("run")]
    public void Constructor_WhenFinishedDeferralsAreInvalid_RejectsExactEnvelopeArgument(string invalid)
    {
        var result = RunResultTestData.Finished();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var request = RunResultTestData.Deferred();
        ImmutableArray<DeferredOperationRequest> requests = invalid switch
        {
            "default" => default,
            "null" => [null!],
            "duplicate" => [request, request],
            "session" => [RunResultTestData.Deferred(session: new SessionId(id))],
            _ => [RunResultTestData.Deferred(run: new RunId(id))],
        };
        var exception = Should.Throw<ArgumentException>(() => new AgentRunFinished<string>(result.AgentId, result.SessionId, null, result.RunId,
            result.Outcome, result.Settlement, result.Output, result.PreviousCursor, [], result.Usage, requests, result.Metadata));
        exception.ParamName.ShouldBe("deferredRequests");
        exception.GetType().ShouldBe(invalid == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsCleanSuccess_WhenSemanticCompletionIsIdleOrSuccessful_RequiresSuccess(bool success)
    {
        AgentRunOutcome outcome = success ? new RunSucceeded() : new RunIdle();
        RunResultTestData.Finished(outcome).IsCleanSuccess.ShouldBe(success);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("policy")]
    [InlineData("limit")]
    public void IsCleanSuccess_WhenSemanticOutcomeHalts_RejectsCleanSuccess(string kind)
    {
        var error = RunResultTestData.Error(AgentErrorCodes.InternalFailure);
        AgentRunOutcome outcome = kind switch
        {
            "failure" => new RunFailed(new(error)),
            "policy" => new RunPolicyHalted(new(error)),
            _ => new RunLimitReached(new(new(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
                BudgetDimensions.InputTokens, BudgetLimitKind.Hard, 10, 12, 1, new BudgetUnit("tokens"), "exhausted"),
                new ComponentId("budget"), true, SideEffectCertainty.Unknown)),
        };
        RunResultTestData.Finished(outcome).IsCleanSuccess.ShouldBeFalse();
    }
}
