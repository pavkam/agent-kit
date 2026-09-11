// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an accepted run's frozen semantic result after its bounded settlement attempt.</summary>
/// <typeparam name="TOutput">The validated output type. The producer transfers an immutable output snapshot; callers must not mutate referenced application values.</typeparam>
/// <remarks>Structural validation does not prove persistence or settlement. The operation owner freezes output and usage at the terminal transition and constructs this envelope after settlement. Later recovery creates new evidence and never modifies an already returned envelope.</remarks>
public sealed record AgentRunFinished<TOutput>: AgentRunResult<TOutput>
{
    /// <summary>Captures a fully correlated final envelope while keeping semantics and settlement independent.</summary>
    /// <param name="agentId">The nondefault accepted agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="conversationId">The optional nondefault conversation.</param>
    /// <param name="runId">The nondefault accepted run.</param>
    /// <param name="outcome">A nonnull canonical RunSucceeded, RunIdle, RunDeferred, RunCancelled, RunLimitReached, RunPolicyHalted or RunFailed outcome.</param>
    /// <param name="settlement">The nonnull outcome of the bounded settlement attempt.</param>
    /// <param name="output">The validated immutable output snapshot, or no output; nullability follows the selected output definition.</param>
    /// <param name="previousCursor">The nonnull pre-run cursor matching agent, session and conversation.</param>
    /// <param name="newMessages">Initialized, nonnull, uniquely identified committed messages for this address and cursor branch, in commit order. Only system/developer records may omit run identity; present run/turn identities must be valid and run identity must match. Incomplete candidates are forbidden.</param>
    /// <param name="usage">The nonnull frozen usage projection for this run.</param>
    /// <param name="deferredRequests">Initialized unique external requests matching session and run. For RunDeferred, this must equal the outcome's ordered requests.</param>
    /// <param name="metadata">Nonnull immutable final metadata classified by the publisher.</param>
    /// <exception cref="ArgumentOutOfRangeException">A required or present optional identity is default, or a message state is undefined.</exception>
    /// <exception cref="ArgumentNullException">A required collaborator, message, deferred request or message extension bag is null.</exception>
    /// <exception cref="ArgumentException">An outcome is legacy or noncanonical, collections are uninitialized or duplicate, a message part is null, a candidate is incomplete, or correlation, handoff ownership or deferred evidence differs.</exception>
    public AgentRunFinished(AgentId agentId, SessionId sessionId, ConversationId? conversationId, RunId runId,
        AgentRunOutcome outcome, RunSettlementOutcome settlement, TOutput? output, MessageCursor previousCursor,
        ImmutableArray<AgentMessage> newMessages, RunUsage usage, ImmutableArray<DeferredOperationRequest> deferredRequests, ExtensionData metadata)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        if (conversationId is { } conversation) { ArgumentOutOfRangeException.ThrowIfEqual(conversation, default, nameof(conversationId)); }
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotEqual(outcome is RunSucceeded or RunIdle or RunDeferred or RunCancelled or RunLimitReached or RunPolicyHalted or RunFailed, true, nameof(outcome));
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(previousCursor);
        ArgumentException.ThrowIfNotEqual(previousCursor.AgentId, agentId, nameof(previousCursor));
        ArgumentException.ThrowIfNotEqual(previousCursor.SessionId, sessionId, nameof(previousCursor));
        ArgumentException.ThrowIfNotEqual(previousCursor.ConversationId, conversationId, nameof(previousCursor));
        ArgumentException.ThrowIfDefault(newMessages);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentException.ThrowIfNotEqual(usage.RunId, runId, nameof(usage));
        ArgumentException.ThrowIfInvalidExternalDeferrals(deferredRequests, allowEmpty: true);
        ArgumentNullException.ThrowIfNull(metadata);
        HashSet<MessageId> messageIds = [];
        foreach (var message in newMessages)
        {
            ArgumentNullException.ThrowIfNull(message, nameof(newMessages));
            ArgumentOutOfRangeException.ThrowIfEqual(message.Id, default, nameof(newMessages));
            ArgumentException.ThrowIfNotEqual(messageIds.Add(message.Id), true, nameof(newMessages));
            ArgumentException.ThrowIfNotEqual(message.AgentId, agentId, nameof(newMessages));
            ArgumentException.ThrowIfNotEqual(message.SessionId, sessionId, nameof(newMessages));
            ArgumentException.ThrowIfNotEqual(message.ConversationId, conversationId, nameof(newMessages));
            ArgumentException.ThrowIfNotEqual(message.BranchId, previousCursor.BranchId, nameof(newMessages));
            if (message.RunId is { } messageRun)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(messageRun, default, nameof(newMessages));
                ArgumentException.ThrowIfNotEqual(messageRun, runId, nameof(newMessages));
            }
            else { ArgumentException.ThrowIfNotEqual(message is SystemMessage or DeveloperMessage, true, nameof(newMessages)); }
            if (message.TurnId is { } turn) { ArgumentOutOfRangeException.ThrowIfEqual(turn, default, nameof(newMessages)); }
            ArgumentOutOfRangeException.ThrowIfUndefined(message.State, nameof(newMessages));
            ArgumentException.ThrowIfNotEqual(message.State == MessageState.Incomplete, false, nameof(newMessages));
            ArgumentException.ThrowIfContainsNull(message.Parts, nameof(newMessages));
            ArgumentNullException.ThrowIfNull(message.Extensions, nameof(newMessages));
        }
        foreach (var deferred in deferredRequests)
        {
            ArgumentException.ThrowIfNotEqual(deferred.SessionId, sessionId, nameof(deferredRequests));
            ArgumentException.ThrowIfNotEqual(deferred.RunId, runId, nameof(deferredRequests));
        }
        if (outcome is RunDeferred handoff) { ArgumentException.ThrowIfNotEqual(handoff.Requests.SequenceEqual(deferredRequests), true, nameof(deferredRequests)); }
        AgentId = agentId; SessionId = sessionId; ConversationId = conversationId; RunId = runId;
        Outcome = outcome; Settlement = settlement; Output = output; PreviousCursor = previousCursor;
        NewMessages = newMessages; Usage = usage; DeferredRequests = deferredRequests; Metadata = metadata;
    }
    /// <summary>Gets the accepted agent identity.</summary>
    /// <value>A nondefault identity shared by the cursor and new messages.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the owning session identity.</summary>
    /// <value>A nondefault identity shared by history and deferred requests.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the optional conversation without fabricating one.</summary>
    /// <value>A nondefault identity when present.</value>
    public ConversationId? ConversationId { get; }
    /// <summary>Gets the accepted run identity, retained when recovery is required.</summary>
    /// <value>A nondefault run matching usage and deferred evidence.</value>
    public RunId RunId { get; }
    /// <summary>Gets the immutable semantic terminal outcome.</summary>
    /// <value>A canonical nonnull outcome independent of settlement success.</value>
    public AgentRunOutcome Outcome { get; }
    /// <summary>Gets the bounded settlement attempt's separate result.</summary>
    /// <value>Completed settlement or recovery required; it never overwrites semantic output.</value>
    public RunSettlementOutcome Settlement { get; }
    /// <summary>Gets the validated output captured at the terminal transition.</summary>
    /// <value>The producer-owned immutable snapshot or no output according to the selected definition.</value>
    public TOutput? Output { get; }
    /// <summary>Gets the pre-run history watermark.</summary>
    /// <value>A nonnull cursor identifying the branch on which the new messages were committed.</value>
    public MessageCursor PreviousCursor { get; }
    /// <summary>Gets new committed messages in original commit order, without role or trust changes.</summary>
    /// <value>An initialized immutable array; interrupted or suspended committed evidence remains explicit.</value>
    public ImmutableArray<AgentMessage> NewMessages { get; }
    /// <summary>Gets the frozen current usage projection.</summary>
    /// <value>A nonnull snapshot; later accounting corrections cannot mutate it.</value>
    public RunUsage Usage { get; }
    /// <summary>Gets externally owned deferred requests retained at completion.</summary>
    /// <value>An initialized immutable array that never contains runtime-owned suspension.</value>
    public ImmutableArray<DeferredOperationRequest> DeferredRequests { get; }
    /// <summary>Gets final immutable metadata without changing the semantic result.</summary>
    /// <value>A nonnull classified bag prepared before publication.</value>
    public ExtensionData Metadata { get; }
    /// <summary>Gets whether both semantic success and completed settlement were reported.</summary>
    /// <value>True only for RunSucceeded with RunSettlementCompleted; idle and recovery-required results are not clean success.</value>
    public bool IsCleanSuccess => Outcome is RunSucceeded && Settlement is RunSettlementCompleted;
    /// <summary>Compares the complete envelope structurally, using the output type's own value equality.</summary>
    /// <param name="other">The candidate envelope, or null.</param>
    /// <returns>True when identities, outcome, settlement, output, cursor and ordered evidence agree.</returns>
    public bool Equals(AgentRunFinished<TOutput>? other) => other is not null
        && AgentId == other.AgentId && SessionId == other.SessionId && ConversationId == other.ConversationId && RunId == other.RunId
        && Outcome == other.Outcome && Settlement == other.Settlement && EqualityComparer<TOutput?>.Default.Equals(Output, other.Output)
        && PreviousCursor == other.PreviousCursor && NewMessages.SequenceEqual(other.NewMessages) && Usage == other.Usage
        && DeferredRequests.SequenceEqual(other.DeferredRequests) && Metadata == other.Metadata;
    /// <summary>Hashes the envelope consistently with structural equality.</summary>
    /// <returns>A hash over the immutable envelope and ordered evidence.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(AgentId); hash.Add(SessionId); hash.Add(ConversationId); hash.Add(RunId);
        hash.Add(Outcome); hash.Add(Settlement); hash.Add(Output); hash.Add(PreviousCursor);
        foreach (var message in NewMessages) { hash.Add(message); }
        hash.Add(Usage);
        foreach (var request in DeferredRequests) { hash.Add(request); }
        hash.Add(Metadata);
        return hash.ToHashCode();
    }
}
