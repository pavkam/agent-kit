// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports masked absence, denial, audit failure, or mismatched operation state.</summary>
public sealed record SessionRunStateUnavailable: SessionRunStateResult
{
    /// <summary>Initializes a safe unavailable result.</summary><param name="safeReason">A nonblank content-free explanation.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunStateUnavailable(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets safe explanation.</summary><value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
