// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports denial, not-found masking, audit failure, or store unavailability before acceptance.</summary>
public sealed record SessionRunStartRejected: SessionRunStartResult
{
    /// <summary>Initializes a safe rejection.</summary><param name="safeReason">A nonblank content-free explanation.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionRunStartRejected(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets safe explanation.</summary><value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
