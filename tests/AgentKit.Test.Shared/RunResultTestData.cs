// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>Supplies deterministic, consistently correlated run-result evidence for contract and stream tests.</summary>
public static class RunResultTestData
{
    /// <summary>Gets the fixed accepted agent used by these fixtures.</summary><value>A nondefault test identity.</value>
    public static AgentId Agent { get; } = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    /// <summary>Gets the fixed owning session.</summary><value>A nondefault test identity.</value>
    public static SessionId Session { get; } = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    /// <summary>Gets the fixed accepted run.</summary><value>A nondefault test identity.</value>
    public static RunId Run { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    /// <summary>Gets the fixed history branch.</summary><value>A nondefault test identity.</value>
    public static BranchId Branch { get; } = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    /// <summary>Gets the fixed turn.</summary><value>A nondefault test identity.</value>
    public static TurnId Turn { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    /// <summary>Gets a fresh immutable pre-run cursor on the fixture branch.</summary><value>A consistently correlated empty-history watermark.</value>
    public static MessageCursor Cursor => new(Agent, Session, null, Branch, default, default);
    /// <summary>Creates a safe normalized test error.</summary><param name="code">The exact error category.</param><returns>Immutable error evidence with unknown effects.</returns>
    public static AgentError Error(AgentErrorCode code) => new(code, "test failure", false, SideEffectCertainty.Unknown,
        new ErrorOrigin("test"), null, null, null, null, ExtensionData.Empty);
    /// <summary>Creates one committed user message for the fixture run.</summary><returns>A complete, correlated immutable message.</returns>
    public static UserMessage Message() => new(new MessageId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
        Agent, Session, null, Branch, Run, Turn, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("submitted", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
    /// <summary>Creates normalized operation evidence without real host effects.</summary><returns>A bounded test-only state-read descriptor.</returns>
    public static ProtectedOperation ProtectedOperation() => new(new ComponentId("test"), SecurityOperationKind.StateRead,
        SecurityEffect.Observe, [new ProtectedResource(ProtectedResourceKind.ApplicationState, "test-session")]);
    /// <summary>Creates a reference to deterministic test-only security decision evidence.</summary><returns>A nonauthorizing immutable decision reference.</returns>
    public static SecurityDecisionReference Decision() => new(new SecurityRequestId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
        new(new SecurityPolicySnapshotId(Guid.Parse("80000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("sha256:test")),
        new SecurityAuditRecordId(Guid.Parse("90000000-0000-0000-0000-000000000001")));
    /// <summary>Creates a deferred request with caller-selected ownership and deterministic correlation.</summary>
    /// <param name="identity">A positive request discriminator.</param><param name="kind">The deferral reason.</param><param name="owner">The continuation owner.</param><param name="effects">Whether execution started.</param><param name="session">An optional session override.</param><param name="run">An optional run override.</param>
    /// <returns>The structurally validated fixture request.</returns><exception cref="ArgumentOutOfRangeException">The discriminator is not positive.</exception>
    public static DeferredOperationRequest Deferred(int identity = 1, DeferralKind kind = DeferralKind.OperationDeferred,
        DeferralContinuationOwner owner = DeferralContinuationOwner.ExternalWorkflow, DeferralEffectState effects = DeferralEffectState.NotStarted,
        SessionId? session = null, RunId? run = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identity);
        return new(new DeferredRequestId(new Guid(identity, 0, 0, new byte[8])), session ?? Session, run ?? Run,
            new OperationId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), kind, owner, effects, ProtectedOperation(),
            new InputFingerprint("sha256:input"), Decision(), DateTimeOffset.UnixEpoch, null, default, ExtensionData.Empty);
    }
    /// <summary>Creates a final string envelope on the fixture run without claiming any actual persistence.</summary>
    /// <param name="outcome">The semantic outcome, defaulting to success.</param><param name="settlement">The settlement evidence, defaulting to completed.</param><param name="messages">Committed messages, defaulting to empty.</param><param name="deferred">External deferred requests, defaulting to empty.</param>
    /// <returns>A validated immutable final envelope with test output.</returns>
    public static AgentRunFinished<string> Finished(AgentRunOutcome? outcome = null, RunSettlementOutcome? settlement = null,
        ImmutableArray<AgentMessage> messages = default, ImmutableArray<DeferredOperationRequest> deferred = default) =>
        new(Agent, Session, null, Run, outcome ?? new RunSucceeded(), settlement ?? new RunSettlementCompleted(), "output", Cursor,
            messages.IsDefault ? [] : messages, new RunUsage(Run, []), deferred.IsDefault ? [] : deferred, ExtensionData.Empty);
    /// <summary>Creates one durable fixture event at a caller-supplied sequence.</summary><param name="sequence">The positive event sequence.</param><returns>A correlated message-commit event.</returns>
    public static RunEvent Event(long sequence) => new MessageCommittedEvent(Agent, Session, null, Run, Turn, sequence,
        DateTimeOffset.UnixEpoch, Message().Id, new SessionVersion(sequence));
}
