// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the named execution lane has never been provisioned on this session.</summary>
/// <remarks>A caller receiving this result may proceed to provision the lane itself.</remarks>
public sealed record SessionLaneStateNotProvisioned: SessionLaneStateResult
{
    /// <summary>Initializes a safe not-provisioned result.</summary>
    /// <param name="safeReason">A nonblank content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionLaneStateNotProvisioned(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the safe explanation.</summary>
    /// <value>A nonblank content-free reason.</value>
    public string SafeReason { get; }
}
