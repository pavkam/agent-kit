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
    public ValueTask<SessionBranchResult> BranchAsync(
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
