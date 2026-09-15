// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Persists one session's append-only record: creation, load, conditional
/// append, forward pagination, branching, and deletion.
/// </summary>
/// <remarks>
/// <para>
/// AgentKit defines no session-store base class. In-memory, SQLite, and
/// remote stores have materially different transaction, serialization, and
/// ownership mechanics; they implement this interface directly and are
/// proven equivalent by running the same conformance suite.
/// </para>
/// <para>
/// Every method is asynchronous and cancellable. Implementations must
/// honor <see cref="SessionAppendRequest.IdempotencyKey"/>,
/// <see cref="SessionCreateRequest.IdempotencyKey"/>,
/// <see cref="SessionBranchRequest.IdempotencyKey"/>, and
/// <see cref="SessionDeleteRequest.IdempotencyKey"/> so that a retried call
/// after an ambiguous failure (timeout, disconnect) never duplicates an
/// effect, and must honor <see cref="SessionAppendRequest.ExpectedVersion"/>
/// so that concurrent writers cannot silently overwrite one another.
/// </para>
/// </remarks>
public interface ISessionStore
{
    /// <summary>Gets the sole security audience permitted to consume grants for this store.</summary>
    /// <value>A stable non-default component identity used by exact enforcement.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets this store's descriptor.</summary>
    public SessionStoreDescriptor Descriptor { get; }

    /// <summary>
    /// Creates a new session, or idempotently returns an existing one
    /// created by an identical prior request.
    /// </summary>
    /// <param name="request">The creation request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionCreateResult> CreateAsync(
        AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Loads one session's current descriptor.</summary>
    /// <param name="context">The operation context identifying the session.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionLoadResult> LoadAsync(
        AuthorizedSessionStoreRequest<SessionOperationContext> context,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically provisions a previously absent execution lane on one exact unowned branch tip.</summary>
    /// <param name="request">The exact protected provisioning request.</param><param name="cancellationToken">Cancels before the atomic mutation begins.</param><returns>The committed lane evidence or a typed no-mutation outcome.</returns>
    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
        AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one or more entries to a branch, conditioned on an expected
    /// version.
    /// </summary>
    /// <param name="request">The append request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionAppendResult> AppendAsync(
        AuthorizedSessionStoreRequest<SessionAppendRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one forward page of entries from a branch.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionPageResult> ReadAsync(
        AuthorizedSessionStoreRequest<SessionReadRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new branch forking from an exact point in an existing
    /// branch.
    /// </summary>
    /// <param name="request">The branch creation request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionBranchResult> CreateBranchAsync(
        AuthorizedSessionStoreRequest<SessionBranchRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one session and its entire record.</summary>
    /// <param name="request">The deletion request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        AuthorizedSessionStoreRequest<SessionDeleteRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up an existing admission before preprocessing or capacity reservation.</summary>
    /// <param name="request">The exact protected lookup request.</param><param name="cancellationToken">Cancels before protected access begins.</param><returns>The terminal replay, conflict, absence, or rejection result.</returns>
    public ValueTask<SessionInputLookupResult> LookupInputAsync(
        AuthorizedSessionStoreRequest<SessionInputLookupRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically admits preprocessed input or reconciles an equivalent prior admission.</summary>
    /// <param name="request">The exact protected admission request.</param><param name="cancellationToken">Cancels before the atomic mutation begins.</param><returns>The accepted receipt, conflict, capacity rejection, or protected-access rejection.</returns>
    public ValueTask<InputAdmissionResult> AdmitInputAsync(
        AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically installs complete accepted run state and consumes its selected pending inputs.</summary>
    /// <param name="request">The exact protected idle-lane start request.</param><param name="cancellationToken">Cancels before the atomic mutation begins.</param><returns>The accepted receipt or a typed no-mutation outcome.</returns>
    public ValueTask<SessionRunStartResult> AcceptRunAsync(
        AuthorizedSessionStoreRequest<SessionRunStartRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Loads complete accepted state for one exact authorized operation.</summary>
    /// <param name="request">The exact protected state request.</param><param name="cancellationToken">Cancels before protected access begins.</param><returns>The complete state or a typed unavailable outcome.</returns>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(
        AuthorizedSessionStoreRequest<SessionRunStateRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically clears one lane's installed accepted run state, freeing it for a later start.</summary>
    /// <param name="request">The exact protected release request naming the lane and the accepted run it owns.</param><param name="cancellationToken">Cancels before the atomic mutation begins.</param><returns>The released receipt or a typed no-mutation outcome.</returns>
    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
        AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request,
        CancellationToken cancellationToken = default);
}
