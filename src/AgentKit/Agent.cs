// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An immutable, engine-bound handle over one validated agent definition.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="Agent"/> owns no mutable session or run state. It is safe to cache and to use concurrently:
/// every invocation creates its own isolated run scope, so two callers running the same agent never share
/// anything but the immutable definition.
/// </para>
/// <para>
/// The handle exposes no service provider. Callers address work through <see cref="RunAsync{TOutput}"/>,
/// <see cref="StreamAsync{TOutput}"/>, and <see cref="SendAsync"/>. New admissions revalidate the exact pinned
/// definition: unrelated catalog updates remain admissible, while a removal or replacement is rejected.
/// </para>
/// </remarks>
public sealed class Agent
{
    private readonly AgentEngineRuntime _runtime;

    /// <summary>Initializes a handle over one resolved definition.</summary>
    /// <param name="runtime">The runtime that owns this handle's composition and admission.</param>
    /// <param name="definition">The validated definition this handle runs.</param>
    /// <param name="catalogVersion">The catalog revision the definition was resolved from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> or <paramref name="definition"/> is null.</exception>
    internal Agent(AgentEngineRuntime runtime, AgentDefinition definition, AgentCatalogVersion catalogVersion)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(definition);
        _runtime = runtime;
        Definition = definition;
        CatalogVersion = catalogVersion;
    }

    /// <summary>Gets this agent's stable identity.</summary>
    public AgentId Id => Definition.Id;

    /// <summary>Gets the immutable definition this handle runs.</summary>
    public AgentDefinition Definition { get; }

    /// <summary>Gets the catalog revision this handle's definition was resolved from.</summary>
    public AgentCatalogVersion CatalogVersion { get; }

    /// <summary>Creates a new session for this agent without admitting any turn.</summary>
    /// <param name="identity">The already-authenticated identity that will own the created session.</param>
    /// <param name="idempotencyKey">The key making a retried creation attempt idempotent.</param>
    /// <param name="conversationId">The optional conversation the new session correlates with.</param>
    /// <param name="extensions">Caller-supplied forward-compatible session data, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token that cancels the creation.</param>
    /// <returns>
    /// <see cref="AgentSessionCreated"/> naming the new or existing session, or <see cref="AgentSessionCreationFailed"/>
    /// with safe, closed evidence.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
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
        _runtime.CreateSessionAsync(
            new AgentSessionCreateRequest(Id, identity, conversationId, idempotencyKey, extensions ?? ExtensionData.Empty),
            cancellationToken);

    /// <summary>
    /// Runs this agent against an existing session and awaits settlement.
    /// </summary>
    /// <typeparam name="TOutput">The validated output type. <see cref="string"/> projects committed assistant text.</typeparam>
    /// <param name="sessionId">The session this run reads from and commits to. The active branch is selected for the caller.</param>
    /// <param name="identity">The already-authenticated identity. AgentKit consumes it and never authenticates it.</param>
    /// <param name="input">The input to admit before the loop starts.</param>
    /// <param name="conversationId">
    /// Accepted for caller convenience. The finished envelope uses the conversation stored on the session.
    /// </param>
    /// <param name="options">Narrowing overrides, or <see langword="null"/> for the definition defaults.</param>
    /// <param name="executionLaneId">The lane to advance, or <see langword="null"/> to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels the wait. Cancellation before acceptance leaves no run identity.</param>
    /// <returns>
    /// <see cref="AgentRunFinished{TOutput}"/> after settlement, or <see cref="AgentRunRejected{TOutput}"/> when
    /// admission fails before acceptance. Rejection does not allocate a <see cref="RunId"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> or <paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> is default, or an override widens the definition.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before acceptance.</exception>
    /// <remarks>
    /// A <see cref="SessionBusyBehavior.Reject"/> profile returns <see cref="AgentRunRejected{TOutput}"/> and appends
    /// nothing. <see cref="SessionBusyBehavior.Wait"/> serializes the caller until the active run releases the
    /// in-process gate. This method does not throw <see cref="AgentAdmissionRejectedException"/>.
    /// </remarks>
    public Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ConversationId? conversationId = null,
        AgentRunOptions? options = null,
        ExecutionLaneId? executionLaneId = null,
        CancellationToken cancellationToken = default) =>
        _runtime.RunAsync<TOutput>(
            this, sessionId, conversationId, identity, input, options, executionLaneId, cancellationToken);

    /// <summary>
    /// Subscribes to this agent's run before the loop is driven. The stream's completion is the settled result.
    /// </summary>
    /// <typeparam name="TOutput">The validated output type.</typeparam>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="identity">The already-authenticated identity.</param>
    /// <param name="input">The input to admit.</param>
    /// <param name="conversationId">Accepted for caller convenience. The session's stored conversation is used.</param>
    /// <param name="options">Narrowing overrides, or <see langword="null"/>.</param>
    /// <param name="executionLaneId">The lane to advance, or <see langword="null"/> to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels admission and the drive. Disposing the stream does not.</param>
    /// <returns>
    /// <see cref="AgentRunStreamStarted{TOutput}"/> when the composed publisher implements
    /// <see cref="ISubscribableOutputPublisher"/>, or <see cref="AgentRunStreamRejected{TOutput}"/> with no run
    /// identity when it does not or when admission fails first.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> or <paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sessionId"/> is default, or an override widens the definition.</exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before acceptance.</exception>
    public Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ConversationId? conversationId = null,
        AgentRunOptions? options = null,
        ExecutionLaneId? executionLaneId = null,
        CancellationToken cancellationToken = default) =>
        _runtime.StreamAsync<TOutput>(
            this, sessionId, conversationId, identity, input, options, executionLaneId, cancellationToken);

    /// <summary>
    /// Sends one user turn: the runtime creates or opens the session, takes its lane, records the message, and
    /// runs the agent to a terminal outcome.
    /// </summary>
    /// <param name="request">The identity, message, session selection, overrides, and observer for the turn.</param>
    /// <param name="cancellationToken">
    /// Cancels the caller's wait. Before the message is committed nothing durable from the turn is appended and
    /// the exception propagates; afterwards the run settles with a typed cancelled outcome that is returned.
    /// </param>
    /// <returns>
    /// The terminal loop result. <see cref="AgentLoopResult.SessionId"/> is the session to pass back as
    /// <see cref="AgentSendRequest.SessionId"/> to continue the conversation.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An override is wider than the definition's default.</exception>
    /// <exception cref="AgentAdmissionRejectedException">
    /// The definition is no longer enabled, the session could not be created, opened, or appended to, or the
    /// session is already running a turn and its profile rejects concurrent turns.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    /// <remarks>
    /// This is a wrapper over the same admission protocol as <see cref="RunAsync{TOutput}"/>. It keeps the
    /// existing exception behavior so callers that already catch <see cref="AgentAdmissionRejectedException"/>
    /// do not gain a second protocol. Prefer <see cref="RunAsync{TOutput}"/> when a typed rejection is required.
    /// </remarks>
    public Task<AgentLoopResult> SendAsync(AgentSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _runtime.SendAsync(this, request, cancellationToken);
    }

    /// <summary>Admits steering input for the next safe boundary of the session's current or next run.</summary>
    /// <param name="sessionId">The session whose lane receives the input. The session must already exist.</param>
    /// <param name="identity">The already-authenticated caller.</param>
    /// <param name="input">Steering input. Follow-up input is rejected.</param>
    /// <param name="executionLaneId">The lane to admit into, or <see langword="null"/> to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels the wait before admission commits.</param>
    /// <returns>The durable admission result. Acceptance does not start a run or append history.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> or <paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> or a present <paramref name="executionLaneId"/> is default, or
    /// <paramref name="input"/> is not <see cref="InputDelivery.Steer"/>.
    /// </exception>
    /// <exception cref="AgentAdmissionRejectedException">The session, definition, authorization, or input coordinator is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before admission committed.</exception>
    public Task<InputAdmissionResult> SteerAsync(
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ExecutionLaneId? executionLaneId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNotEqual(input.Delivery, InputDelivery.Steer, nameof(input));
        return _runtime.AdmitInputAsync(this, sessionId, identity, input, executionLaneId, cancellationToken);
    }

    /// <summary>Admits follow-up input that becomes eligible only when current work would otherwise finish.</summary>
    /// <param name="sessionId">The session whose lane receives the input. The session must already exist.</param>
    /// <param name="identity">The already-authenticated caller.</param>
    /// <param name="input">Follow-up input. Steering input is rejected.</param>
    /// <param name="executionLaneId">The lane to admit into, or <see langword="null"/> to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels the wait before admission commits.</param>
    /// <returns>The durable admission result. Acceptance does not start a run or append history.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> or <paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> or a present <paramref name="executionLaneId"/> is default, or
    /// <paramref name="input"/> is not <see cref="InputDelivery.FollowUp"/>.
    /// </exception>
    /// <exception cref="AgentAdmissionRejectedException">The session, definition, authorization, or input coordinator is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before admission committed.</exception>
    public Task<InputAdmissionResult> FollowUpAsync(
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ExecutionLaneId? executionLaneId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNotEqual(input.Delivery, InputDelivery.FollowUp, nameof(input));
        return _runtime.AdmitInputAsync(this, sessionId, identity, input, executionLaneId, cancellationToken);
    }

    /// <summary>Requests durable abort for one active run in this process.</summary>
    /// <param name="runId">The accepted run to abort.</param>
    /// <param name="identity">The already-authenticated caller.</param>
    /// <param name="cancellationToken">Cancels the wait before the abort commits.</param>
    /// <returns>The session store's typed abort outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default.</exception>
    /// <exception cref="AgentAdmissionRejectedException">The run is not active in this process or the session is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    public Task<SessionRunAbortResult> CancelAsync(
        RunId runId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        return _runtime.CancelAsync(this, runId, identity, cancellationToken);
    }

    /// <summary>Attaches to one active run's durable replay and live event tail.</summary>
    /// <typeparam name="TOutput">The validated output snapshot type.</typeparam>
    /// <param name="runId">The accepted run to attach to.</param>
    /// <param name="sessionId">The session the run belongs to; used for typed rejection evidence when attach is unavailable.</param>
    /// <param name="identity">The already-authenticated caller.</param>
    /// <param name="cancellationToken">Cancels attachment setup. It does not abort the run.</param>
    /// <returns>A started stream, or a rejection when the run settled or cannot be tailed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> or <paramref name="sessionId"/> is default.</exception>
    /// <exception cref="ObjectDisposedException">The owning engine has been disposed.</exception>
    public Task<AgentRunStreamStartResult<TOutput>> AttachAsync<TOutput>(
        RunId runId,
        SessionId sessionId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        return _runtime.AttachAsync<TOutput>(this, runId, sessionId, identity, cancellationToken);
    }
}
