// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports masked absence, denial, or audit failure for one lane-state discovery request.</summary>
/// <remarks>
/// This result never distinguishes an unknown session from a denied one, so an unauthorized caller cannot learn
/// which reason applies.
/// </remarks>
public sealed record SessionLaneStateUnavailable: SessionLaneStateResult
{
    /// <summary>Initializes a safe unavailable result.</summary>
    /// <param name="safeReason">A nonblank content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionLaneStateUnavailable(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the safe explanation.</summary>
    /// <value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
