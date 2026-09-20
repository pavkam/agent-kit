// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed reason a durable abort did not record a cancel marker or prune admissions.</summary>
/// <remarks>
/// A session or lane absent from the caller's tenant reports <see cref="SessionRunAbortRejectionKind.LaneNotFound"/>,
/// the same masking every other protected session operation uses for cross-tenant or missing addresses. Rejection
/// appends nothing and prunes nothing.
/// </remarks>
public sealed record SessionRunAbortRejected: SessionRunAbortResult
{
    /// <summary>Initializes a typed abort rejection.</summary>
    /// <param name="kind">The defined rejection class.</param>
    /// <param name="safeReason">A nonblank content-free explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunAbortRejected(SessionRunAbortRejectionKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind;
        SafeReason = safeReason;
    }

    /// <summary>Gets the rejection class.</summary>
    /// <value>A defined machine-readable precondition failure.</value>
    public SessionRunAbortRejectionKind Kind { get; }

    /// <summary>Gets the safe explanation.</summary>
    /// <value>A nonblank content-free reason. It never includes caller payloads, prompts, or tool content.</value>
    public string SafeReason { get; }
}
