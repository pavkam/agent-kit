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
        SessionCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Loads one session's current descriptor.</summary>
    /// <param name="context">The operation context identifying the session.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one or more entries to a branch, conditioned on an expected
    /// version.
    /// </summary>
    /// <param name="request">The append request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one forward page of entries from a branch.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new branch forking from an exact point in an existing
    /// branch.
    /// </summary>
    /// <param name="request">The branch creation request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionBranchResult> CreateBranchAsync(
        SessionBranchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one session and its entire record.</summary>
    /// <param name="request">The deletion request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request,
        CancellationToken cancellationToken = default);
}
