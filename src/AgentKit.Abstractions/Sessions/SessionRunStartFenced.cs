// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports absent, stale, or unsupported distributed ownership evidence.</summary>
public sealed record SessionRunStartFenced: SessionRunStartResult
{
    /// <summary>Initializes a fenced result.</summary><param name="safeReason">A nonblank content-free explanation.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunStartFenced(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets safe explanation.</summary><value>A nonblank reason that exposes no fencing secret.</value>
    public string SafeReason { get; }
}
