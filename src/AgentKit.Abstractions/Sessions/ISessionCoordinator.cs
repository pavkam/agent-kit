// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Coordinates session lifecycle operations against the configured store:
/// authorization orchestration, routing, optimistic-conflict handling, and
/// semantic events. It does not implement storage itself.
/// </summary>
/// <remarks>
/// The coordinator never invokes the agent loop, input coordinator, context
/// assembler, or a durability coordinator; the engine acquires a run lease
/// through <see cref="ISessionRunCoordinator"/> and then calls the loop
/// itself, which avoids a session-to-loop-to-session dependency cycle.
/// </remarks>
public interface ISessionCoordinator
{
    /// <summary>
    /// Creates a new session, or idempotently returns an existing one
    /// created by an identical prior request.
    /// </summary>
    /// <param name="request">The creation request.</param>
    /// <param name="profile">The exact immutable session profile selected for this operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    /// <summary>Loads one session's current descriptor.</summary>
    /// <param name="context">The operation context identifying the session.</param>
    /// <param name="profile">The exact immutable session profile selected for this operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    /// <summary>Lists one bounded page of visible sessions without probing concrete stores.</summary>
    /// <param name="request">The sessionless authorized discovery request.</param>
    /// <param name="cancellationToken">Cancels the directory operation.</param>
    /// <returns>The visible page or a typed unavailable result.</returns>
    public ValueTask<SessionDirectoryListResult> ListAsync(
        SessionDirectoryListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionDirectoryListResult>(
            new SessionDirectoryListUnavailable("This session coordinator does not support bounded discovery."));
    }

    /// <summary>
    /// Appends one or more entries to a branch, conditioned on an expected
    /// version.
    /// </summary>
    /// <param name="request">The append request.</param>
    /// <param name="profile">The exact immutable session profile selected for this operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one forward page of entries from a branch.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="profile">The exact immutable session profile selected for this operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new branch forking from an exact point in an existing
    /// branch.
    /// </summary>
    /// <param name="request">The branch creation request.</param>
    /// <param name="profile">The exact immutable session profile selected for this operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one session and its entire record.</summary>
    /// <param name="request">The deletion request.</param>
    /// <param name="profile">The exact immutable session profile selected for this operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up a previously accepted input before preprocessing is repeated.</summary>
    /// <param name="request">The exact input lookup request.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing replay evidence or a typed absence/failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<SessionInputLookupResult> LookupInputAsync(
        SessionInputLookupRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionInputLookupResult>(
            new SessionInputLookupRejected("The coordinator does not support protected admitted-input lookup."));
    }

    /// <summary>Atomically provisions one execution lane on an exact branch cursor.</summary>
    /// <param name="request">The lane provisioning transaction.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the committed lane or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
        SessionExecutionLaneProvisionRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
            new SessionExecutionLaneProvisionRejected("The coordinator does not support protected lane provisioning."));
    }

    /// <summary>Loads one provisioned execution lane's current durable revision, branch cursor, and optional accepted run.</summary>
    /// <param name="request">The exact lane-state discovery request.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the lane's current state, a typed not-provisioned result, or typed unavailability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(
        SessionLaneStateRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionLaneStateResult>(
            new SessionLaneStateUnavailable("The coordinator does not support protected lane-state discovery."));
    }

    /// <summary>Atomically admits one preprocessed input into durable pending state.</summary>
    /// <param name="request">The complete input admission transaction.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing accepted replay evidence or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<InputAdmissionResult> AdmitInputAsync(
        SessionInputAdmissionRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<InputAdmissionResult>(
            new RejectedInput(new InputRejection(InputRejectionKind.Unauthorized,
                "The coordinator does not support protected input admission.")));
    }

    /// <summary>Atomically consumes selected pending input and installs complete accepted run state.</summary>
    /// <param name="request">The complete run acceptance transaction.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing complete accepted state or a typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<SessionRunStartResult> AcceptRunAsync(
        SessionRunStartRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionRunStartResult>(
            new SessionRunStartRejected("The coordinator does not support protected run acceptance."));
    }

    /// <summary>Loads complete accepted state for exact recovery or ownership revalidation.</summary>
    /// <param name="request">The exact lane-bound in-run state request.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing complete accepted state or typed unavailability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(
        SessionRunStateRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionRunStateResult>(
            new SessionRunStateUnavailable("The coordinator does not support protected run-state loading."));
    }

    /// <summary>Atomically clears one lane's installed accepted run state, freeing it for a later start.</summary>
    /// <param name="request">The complete lane-release transaction.</param>
    /// <param name="session">The compiled invocation capability selecting this coordinator and immutable profile.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the released receipt or a typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
        SessionRunReleaseRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionRunReleaseResult>(
            new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Unsupported,
                "The coordinator does not support protected run release."));
    }
}
