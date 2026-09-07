// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Ownership was refused because another worker currently holds an unexpired
/// lease for the operation.
/// </summary>
/// <remarks>
/// This is a normal, expected outcome rather than an error. Exactly one
/// worker may own an operation, so every other contender is told to wait. The
/// reported expiry lets a caller schedule a later attempt through injected
/// time instead of polling.
/// </remarks>
public sealed record ExecutionLeaseHeldByAnotherWorker: ExecutionLeaseResult
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ExecutionLeaseHeldByAnotherWorker"/> record.
    /// </summary>
    /// <param name="currentOwnerWorkerId">
    /// The worker that currently holds ownership.
    /// </param>
    /// <param name="currentToken">
    /// The authoritative ownership generation held by that worker.
    /// </param>
    /// <param name="expiresAt">
    /// The instant the current lease expires if it is not renewed, after
    /// which takeover may succeed.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="currentOwnerWorkerId"/> is its default, empty
    /// identity, or <paramref name="currentToken"/> is the default,
    /// unallocated token.
    /// </exception>
    public ExecutionLeaseHeldByAnotherWorker(
        WorkerId currentOwnerWorkerId,
        FencingToken currentToken,
        DateTimeOffset expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            currentOwnerWorkerId,
            default,
            nameof(currentOwnerWorkerId));
        ArgumentOutOfRangeException.ThrowIfEqual(currentToken, default, nameof(currentToken));

        CurrentOwnerWorkerId = currentOwnerWorkerId;
        CurrentToken = currentToken;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the worker that currently holds ownership.</summary>
    public WorkerId CurrentOwnerWorkerId { get; }

    /// <summary>Gets the authoritative ownership generation.</summary>
    public FencingToken CurrentToken { get; }

    /// <summary>Gets the instant the current lease expires if not renewed.</summary>
    /// <value>
    /// An instant produced from the lease manager's injected
    /// <see cref="TimeProvider"/>, so waiting is deterministic in tests.
    /// </value>
    public DateTimeOffset ExpiresAt { get; }
}
