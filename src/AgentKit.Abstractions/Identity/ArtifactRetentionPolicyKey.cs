// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one configured artifact retention policy.</summary>
public readonly record struct ArtifactRetentionPolicyKey
{
    /// <summary>Initializes a non-blank policy key.</summary>
    /// <param name="value">The stable policy value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactRetentionPolicyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    /// <summary>Gets the stable policy value.</summary>
    public string Value { get; }
    /// <summary>Returns the stable policy value.</summary>
    public override string ToString() => Value;
}
