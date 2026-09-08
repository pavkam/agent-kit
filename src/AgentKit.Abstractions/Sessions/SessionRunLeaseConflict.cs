// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that protected accepted-state revalidation did not match the requested lane owner.</summary>
public sealed record SessionRunLeaseConflict: SessionRunLeaseResult
{
    /// <summary>Initializes a bounded conflict result.</summary>
    /// <param name="kind">The defined conflict category.</param>
    /// <param name="safeReason">A nonblank content-free explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunLeaseConflict(SessionRunLeaseConflictKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind;
        SafeReason = safeReason;
    }

    /// <summary>Gets the conflict category.</summary><value>A defined bounded category.</value>
    public SessionRunLeaseConflictKind Kind { get; }
    /// <summary>Gets the safe explanation.</summary><value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
