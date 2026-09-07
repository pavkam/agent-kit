// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The journal rejected the write because the writer's ownership generation
/// is older than the current authoritative one; the caller's lease was taken
/// over.
/// </summary>
/// <remarks>
/// <para>
/// This is the outcome that makes distributed durability safe. A worker that
/// paused long enough for its lease to expire will still try to write; the
/// store must refuse it rather than let it overwrite the state of the worker
/// that replaced it.
/// </para>
/// <para>
/// A fenced writer must stop immediately. It has lost authority and must not
/// retry the write, renew, or perform further effects for the operation.
/// </para>
/// </remarks>
public sealed record DurableRecordFenced: DurableRecordResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableRecordFenced"/>
    /// record.
    /// </summary>
    /// <param name="presentedToken">
    /// The stale ownership generation the caller presented.
    /// </param>
    /// <param name="currentToken">
    /// The current authoritative ownership generation held by another worker.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="presentedToken"/> or <paramref name="currentToken"/>
    /// is the default, unallocated token, or
    /// <paramref name="currentToken"/> is not newer than
    /// <paramref name="presentedToken"/>. A fencing rejection is only
    /// meaningful when the presented generation is genuinely older.
    /// </exception>
    public DurableRecordFenced(FencingToken presentedToken, FencingToken currentToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(presentedToken, default, nameof(presentedToken));
        ArgumentOutOfRangeException.ThrowIfEqual(currentToken, default, nameof(currentToken));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            currentToken.Value,
            presentedToken.Value,
            nameof(currentToken));

        PresentedToken = presentedToken;
        CurrentToken = currentToken;
    }

    /// <summary>
    /// Gets the stale ownership generation the caller presented.
    /// </summary>
    public FencingToken PresentedToken { get; }

    /// <summary>
    /// Gets the current authoritative ownership generation.
    /// </summary>
    public FencingToken CurrentToken { get; }
}
