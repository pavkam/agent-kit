// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed reason a lane-release request did not clear any accepted state.</summary>
/// <remarks>A session or lane absent from the caller's tenant reports <see cref="SessionRunReleaseRejectionKind.LaneNotFound"/>, the same masking every other protected session operation uses for cross-tenant or missing addresses; it never distinguishes "does not exist" from "not yours" to an unauthorized caller.</remarks>
public sealed record SessionRunReleaseRejected: SessionRunReleaseResult
{
    /// <summary>Initializes a typed release rejection.</summary>
    /// <param name="kind">The defined rejection class.</param>
    /// <param name="safeReason">A nonblank content-free explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunReleaseRejected(SessionRunReleaseRejectionKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind;
        SafeReason = safeReason;
    }

    /// <summary>Gets the rejection class.</summary><value>A defined machine-readable precondition failure.</value>
    public SessionRunReleaseRejectionKind Kind { get; }

    /// <summary>Gets the safe explanation.</summary><value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
