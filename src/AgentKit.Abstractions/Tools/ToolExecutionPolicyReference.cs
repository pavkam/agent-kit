// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References one exact immutable tool-execution policy revision.</summary>
/// <remarks>The reference is captured evidence; consumers must resolve and revalidate the named policy before use.</remarks>
public sealed record ToolExecutionPolicyReference
{
    /// <summary>Initializes an exact tool-execution policy reference.</summary>
    /// <param name="key">The nondefault policy-family key.</param>
    /// <param name="version">The positive policy revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> or <paramref name="version"/> is default.</exception>
    public ToolExecutionPolicyReference(ToolExecutionPolicyKey key, ToolExecutionPolicyVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        Key = key;
        Version = version;
    }

    /// <summary>Gets the policy-family key.</summary>
    /// <value>A nondefault immutable key.</value>
    public ToolExecutionPolicyKey Key { get; }

    /// <summary>Gets the exact policy revision.</summary>
    /// <value>A positive immutable version.</value>
    public ToolExecutionPolicyVersion Version { get; }
}
