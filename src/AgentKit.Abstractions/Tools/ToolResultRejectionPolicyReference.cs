// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References the exact immutable run-level rejection policy captured before resolution.</summary>
public sealed record ToolResultRejectionPolicyReference
{
    /// <summary>Captures the exact named rejection policy used for unresolved and invalid calls.</summary>
    /// <param name="key">The nondefault ordinal policy-family key.</param>
    /// <param name="version">The positive published policy revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> or <paramref name="version"/> is default.</exception>
    public ToolResultRejectionPolicyReference(
        ToolResultRejectionPolicyKey key,
        ToolResultRejectionPolicyVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        Key = key;
        Version = version;
    }
    /// <summary>Gets the captured policy key.</summary>
    /// <value>A nondefault key.</value>
    public ToolResultRejectionPolicyKey Key { get; }

    /// <summary>Gets the captured policy revision.</summary>
    /// <value>A positive revision.</value>
    public ToolResultRejectionPolicyVersion Version { get; }
}
