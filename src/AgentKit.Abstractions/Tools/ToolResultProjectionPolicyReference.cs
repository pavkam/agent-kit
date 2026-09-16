// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the exact key and revision of a projection policy selected for one result projection.</summary>
/// <remarks>The reference is immutable evidence. Resolving its key or detecting conflicting content belongs to a future policy catalog.</remarks>
public sealed record ToolResultProjectionPolicyReference
{
    /// <summary>Initializes a complete projection-policy reference.</summary>
    /// <param name="key">The nonblank projection-policy key.</param>
    /// <param name="version">The positive published policy revision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> has blank text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public ToolResultProjectionPolicyReference(
        ToolResultProjectionPolicyKey key,
        ToolResultProjectionPolicyVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        Key = key;
        Version = version;
    }

    /// <summary>Gets the selected projection-policy key.</summary>
    /// <value>A nonblank immutable composition key.</value>
    public ToolResultProjectionPolicyKey Key { get; }

    /// <summary>Gets the selected published policy revision.</summary>
    /// <value>A positive immutable revision.</value>
    public ToolResultProjectionPolicyVersion Version { get; }

    /// <summary>Gets the well-known reference used when no richer policy selection has captured a different one.</summary>
    /// <value>A stable reference to the framework's baseline projection policy at its first published revision.</value>
    public static ToolResultProjectionPolicyReference Default { get; } = new(
        new ToolResultProjectionPolicyKey("agentkit.tools.default-projection"),
        new ToolResultProjectionPolicyVersion(1));
}
