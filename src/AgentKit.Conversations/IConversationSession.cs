// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Drives one ongoing conversation: session creation, message admission, and the agent-loop run in one call.</summary>
/// <remarks>
/// <para>
/// Composing a single working conversational turn otherwise requires an application to call
/// <c>ISessionCoordinator</c> to create and load a session, capture authorization through
/// <c>ISecurityProfileSelector</c> twice (once to admit the user's message, once to authorize the run), append a
/// <c>MessageSessionEntry</c> itself, build an <c>AgentLoopRunRequest</c> from its own instructions and tools, and run
/// <c>IAgentLoop</c> directly — all of it bypassing the <c>AgentEngine</c>/<c>Agent</c> facade, which has no method
/// to admit a message into a session before starting a run. <see cref="IConversationSession"/> performs all of
/// that consistently for the common case of one long-lived, single-branch conversation against one composed agent.
/// </para>
/// <para>
/// This is not a replacement for <c>AgentEngine</c>: it does not host a catalog of several agent definitions,
/// does not version or admit definitions, and does not implement the durable, queue-backed input admission
/// <c>AgentKit.IO</c> provides for multi-writer or distributed hosts. It is the direct, in-process composition an
/// application reaches for when it owns its own <see cref="IServiceProvider"/> and wants one conversation with one
/// agent, such as a terminal, desktop, or single-tenant service host.
/// </para>
/// </remarks>
public interface IConversationSession
{
    /// <summary>Gets the durable session this conversation is bound to, once it is bound.</summary>
    /// <value>
    /// <see langword="null"/> until the first successful <c>SendAsync</c> creates the session or <c>OpenAsync</c>
    /// binds an existing one; afterwards the stable identity every later turn is recorded against. Implementations
    /// that do not bind a durable session return <see langword="null"/> permanently.
    /// </value>
    /// <remarks>
    /// Reading this property is safe from any thread and never blocks on an in-flight turn; it reflects the last
    /// completed binding. The same identity is also announced once as a <see cref="ConversationSessionBoundEvent"/>
    /// at the start of the first turn delivered to an <see cref="IConversationEventObserver"/>.
    /// </remarks>
    public SessionId? SessionId => null;

    /// <summary>Gets the active branch every turn of this conversation appends to, once the session is bound.</summary>
    /// <value><see langword="null"/> whenever <see cref="SessionId"/> is <see langword="null"/>; otherwise the bound branch.</value>
    public BranchId? BranchId => null;

    /// <summary>Presents one durable tool call or result using this conversation's captured tool bindings.</summary>
    /// <param name="part">The original immutable tool call or result part.</param>
    /// <param name="cancellationToken">Cancels bounded presentation work.</param>
    /// <returns>The same provider-neutral presentation used for live events, or null when unavailable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="part"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="part"/> is not a tool call or result.</exception>
    public ValueTask<ToolPresentation?> PresentToolAsync(
        ContentPart part,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentException.ThrowIfNotEqual(part is ToolCallPart or ToolResultPart, true, nameof(part));
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ToolPresentation?>(null);
    }

    /// <summary>Reads one bounded forward page of durable messages from this conversation's bound branch.</summary>
    /// <param name="afterSequence">
    /// The stable whole-session sequence after which entries are scanned. Use <c>new SessionSequence(0)</c> for the
    /// first page, then pass the prior page's <see cref="ConversationHistoryPage.NextCursor"/>.
    /// </param>
    /// <param name="maximumEntries">
    /// The positive maximum number of session entries to scan. Operational entries count toward this bound even
    /// though only <see cref="MessageSessionEntry"/> values appear in the returned message projection.
    /// </param>
    /// <param name="cancellationToken">Cancels authorization capture or the authoritative session read.</param>
    /// <returns>
    /// A stable ordered page with continuation evidence, or a content-safe unavailable outcome. The conversation
    /// must already have been opened or created by a successful send.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumEntries"/> is not positive.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <remarks>
    /// This additive default preserves existing implementations: sessions that do not implement durable history
    /// return a typed unavailable result after validating arguments and cancellation.
    /// </remarks>
    public ValueTask<ConversationHistoryReadResult> ReadHistoryAsync(
        SessionSequence afterSequence,
        int maximumEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ConversationHistoryReadResult>(
            new ConversationHistoryUnavailable("This conversation implementation does not support durable history reads."));
    }

    /// <summary>Reopens one existing session before this conversation has created or opened another.</summary>
    /// <param name="sessionId">The non-default session identity.</param>
    /// <param name="cancellationToken">Cancels the authoritative load.</param>
    /// <returns>The opened branch or a content-safe rejection that leaves this instance unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sessionId"/> is default.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ConversationSessionOpenResult> OpenAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ConversationSessionOpenResult>(
            new ConversationSessionOpenRejected("This conversation implementation does not support reopening sessions."));
    }

    /// <summary>Lists one bounded page of sessions visible to this conversation's configured identity and agent.</summary>
    /// <param name="afterSessionId">The exclusive stable continuation cursor.</param>
    /// <param name="maximumResults">The positive page-size bound.</param>
    /// <param name="cancellationToken">Cancels discovery.</param>
    /// <returns>The ordered visible page or a content-safe unavailable outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumResults"/> is not positive.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ConversationSessionListResult> ListAsync(
        SessionId? afterSessionId,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ConversationSessionListResult>(
            new ConversationSessionListUnavailable("This conversation implementation does not support session discovery."));
    }

    /// <summary>Submits one user message, driving session creation, admission, and a full agent-loop run.</summary>
    /// <param name="userText">The nonblank user-authored message text.</param>
    /// <param name="cancellationToken">Cancels the pending turn.</param>
    /// <returns>The committed activity for this turn, or a single explanatory event when admission itself failed.</returns>
    /// <exception cref="ArgumentException"><paramref name="userText"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="InvalidOperationException">Session creation or authorization capture did not succeed.</exception>
    /// <remarks>
    /// Calls to one <see cref="IConversationSession"/> instance are serialized: a call that arrives while a prior
    /// call is still running awaits that prior call's completion rather than interleaving with it, because both
    /// share one session and one active branch. The first call lazily creates the underlying session; every
    /// later call reuses it.
    /// </remarks>
    public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default);

    /// <summary>Submits one user message while forwarding incremental activity to an observer.</summary>
    /// <param name="userText">The nonblank user-authored message text.</param>
    /// <param name="observer">Receives provisional activity and exactly one terminal turn event.</param>
    /// <param name="cancellationToken">Cancels the pending turn and observer delivery.</param>
    /// <returns>The same committed terminal result returned by the non-observing overload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observer"/> is null.</exception>
    /// <remarks>
    /// Implementations that do not override this additive member retain source and binary compatibility. The
    /// default implementation drives the existing overload, then delivers its committed events and one terminal
    /// event. It therefore provides terminal-only fallback rather than claiming live progress. Observer failures
    /// remain isolated from the already completed turn.
    /// </remarks>
    public async Task<ConversationTurnResult> SendAsync(
        string userText,
        IConversationEventObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        try
        {
            var result = await SendAsync(userText, cancellationToken).ConfigureAwait(false);
            foreach (var conversationEvent in result.Events)
            {
                await DeliverFallbackAsync(observer, conversationEvent, cancellationToken).ConfigureAwait(false);
            }

            await DeliverFallbackAsync(
                observer,
                new ConversationTurnCompletedEvent(result.Succeeded, result.Succeeded ? "settled" : "failed"),
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await DeliverFallbackAsync(
                observer,
                new ConversationTurnCompletedEvent(false, "cancelled"),
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            await DeliverFallbackAsync(
                observer,
                new ConversationTurnCompletedEvent(false, "faulted"),
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask DeliverFallbackAsync(
        IConversationEventObserver observer,
        ConversationEvent conversationEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await observer.OnEventAsync(conversationEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Optional presentation cannot change a completed conversation result.
        }
    }
}
