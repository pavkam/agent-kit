// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that protected canonical-state validation could not complete.</summary>
public sealed record SessionRunLeaseUnavailable: SessionRunLeaseResult
{
    /// <summary>Initializes an unavailable result.</summary>
    /// <param name="safeReason">A nonblank content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunLeaseUnavailable(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the safe explanation.</summary><value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
