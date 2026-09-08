// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed optimistic or idempotency conflict with no partial mutation.</summary>
public sealed record SessionRunStartConflict: SessionRunStartResult
{
    /// <summary>Initializes a safe conflict.</summary><param name="kind">The defined conflict class.</param><param name="safeReason">A nonblank content-free explanation.</param><exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunStartConflict(SessionRunStartConflictKind kind, string safeReason) { ArgumentOutOfRangeException.ThrowIfUndefined(kind); ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); Kind = kind; SafeReason = safeReason; }
    /// <summary>Gets conflict class.</summary><value>A defined machine-readable precondition failure.</value>
    public SessionRunStartConflictKind Kind { get; }
    /// <summary>Gets safe explanation.</summary><value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
