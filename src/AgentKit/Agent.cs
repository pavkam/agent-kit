// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An immutable, engine-bound handle over one validated agent definition.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="Agent"/> owns no mutable session or run state. It is safe to
/// cache and to use concurrently: every invocation creates its own isolated
/// run scope, so two callers running the same agent never share anything but
/// the immutable definition.
/// </para>
/// <para>
/// The handle deliberately exposes no service provider. Callers address work
/// through <see cref="RunAsync"/>; they cannot reach into the container,
/// bypass definition validation, or substitute a different loop.
/// </para>
/// <para>
/// The handle captures the definition and catalog version it was resolved
/// with. New admissions revalidate that exact definition against one current
/// catalog snapshot: unrelated updates remain admissible, while a removal or
/// replacement is rejected rather than silently upgrading work.
/// </para>
/// </remarks>
public sealed class Agent
{
    private readonly AgentEngine _engine;

    /// <summary>
    /// Initializes a handle over one resolved definition.
    /// </summary>
    /// <param name="engine">The engine that owns this handle's composition.</param>
    /// <param name="definition">The validated definition this handle runs.</param>
    /// <param name="catalogVersion">
    /// The catalog revision the definition was resolved from.
    /// </param>
    internal Agent(AgentEngine engine, AgentDefinition definition, AgentCatalogVersion catalogVersion)
    {
        Debug.Assert(engine is not null, "A resolved handle always has an owning engine.");
        Debug.Assert(definition is not null, "A resolved handle always has a validated definition.");

        _engine = engine;
        Definition = definition;
        CatalogVersion = catalogVersion;
    }

    /// <summary>Gets this agent's stable identity.</summary>
    public AgentId Id => Definition.Id;

    /// <summary>Gets the immutable definition this handle runs.</summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// Gets the catalog revision this handle's definition was resolved from.
    /// </summary>
    public AgentCatalogVersion CatalogVersion { get; }

    /// <summary>
    /// Runs this agent once in its own isolated run scope, bypassing the session lane protocol.
    /// </summary>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="branchId">The branch this run reads from and commits to.</param>
    /// <param name="identity">
    /// The already-authenticated identity on whose behalf the run is performed. AgentKit consumes this identity;
    /// it never authenticates it.
    /// </param>
    /// <param name="options">The bounded overrides for this invocation, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>
    /// The loop's terminal result, including the committed messages and final
    /// session version.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each call creates a new dependency-injection scope and a new
    /// <see cref="RunId"/>, resolves the selected loop from that scope, and
    /// disposes the scope when the run settles. No mutable run state is
    /// retained on this handle or on the engine.
    /// </para>
    /// <para>
    /// This overload takes no session lane and performs no admission: it never provisions or loads a session, never
    /// enters a session lane, and never checks <see cref="SessionBusyBehavior"/> against a concurrent turn. Prefer
    /// <see cref="SendAsync"/> for ordinary conversational turns; this overload exists for callers that already own
    /// external serialization over the named session and branch.
    /// </para>
    /// <para>
    /// Overrides in <paramref name="options"/> may only narrow the
    /// definition's limits; an attempt to widen them is rejected before the
    /// run starts.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> or <paramref name="branchId"/> is default, or an override in
    /// <paramref name="options"/> is wider than the definition's corresponding default.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The owning engine has been disposed.
    /// </exception>
    /// <exception cref="AgentAdmissionRejectedException">
    /// The current catalog no longer enables this handle's exact pinned
    /// definition, so no run identity, scope, or loop work is created.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public Task<AgentLoopResult> RunAsync(
        SessionId sessionId,
        BranchId branchId,
        ExecutionIdentity identity,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default, nameof(branchId));
        ArgumentNullException.ThrowIfNull(identity);
        return _engine.RunAgentAsync(Definition, sessionId, branchId, identity, options, cancellationToken);
    }

    /// <summary>
    /// Creates a new session for this agent without admitting any turn.
    /// </summary>
    /// <param name="identity">The already-authenticated identity that will own the created session.</param>
    /// <param name="idempotencyKey">The key making a retried creation attempt idempotent.</param>
    /// <param name="conversationId">The optional conversation the new session correlates with.</param>
    /// <param name="extensions">Caller-supplied forward-compatible session data, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token that cancels the creation.</param>
    /// <returns>
    /// <see cref="AgentSessionCreated"/> naming the new (or, for a retried idempotency key, existing) session, or
    /// <see cref="AgentSessionCreationFailed"/> with safe, closed evidence describing why creation did not succeed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="idempotencyKey"/> or a present <paramref name="conversationId"/> is default.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    public Task<AgentSessionCreationResult> CreateSessionAsync(
        ExecutionIdentity identity,
        IdempotencyKey idempotencyKey,
        ConversationId? conversationId = null,
        ExtensionData? extensions = null,
        CancellationToken cancellationToken = default) =>
        _engine.CreateSessionAsync(
            new AgentSessionCreateRequest(Id, identity, conversationId, idempotencyKey, extensions ?? ExtensionData.Empty),
            cancellationToken);

    /// <summary>
    /// Sends one user turn to this agent: the engine creates or opens the session, takes its lane, records the
    /// message, and runs the agent to a terminal outcome.
    /// </summary>
    /// <param name="request">The identity, message, session selection, overrides, and observer for the turn.</param>
    /// <param name="cancellationToken">
    /// Cancels the caller's wait. Before the message is committed nothing durable happens and the exception
    /// propagates; afterwards the run settles with a typed cancelled outcome that is returned.
    /// </param>
    /// <returns>
    /// The terminal result. <see cref="AgentLoopResult.SessionId"/> is the session to pass back as
    /// <see cref="AgentSendRequest.SessionId"/> to continue the conversation, and
    /// <see cref="AgentLoopResult.NewMessages"/> holds every message this turn committed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An override is wider than the definition's default.</exception>
    /// <exception cref="AgentAdmissionRejectedException">The definition is no longer enabled, or the session could not be created, opened, or appended to.</exception>
    /// <exception cref="AgentSessionBusyException">The session is running another turn and its profile rejects concurrent turns.</exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    /// <remarks>
    /// Turns on different sessions, of this or any other agent hosted by the same engine, run concurrently. Turns
    /// on the same session are serialized or rejected by the session profile's <see cref="SessionBusyBehavior"/>.
    /// This handle holds no state between calls and is safe to share.
    /// </remarks>
    public Task<AgentLoopResult> SendAsync(
        AgentSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _engine.SendAgentAsync(Definition, request, cancellationToken);
    }
}
