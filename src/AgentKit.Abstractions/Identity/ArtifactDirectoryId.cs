// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one logical artifact directory independently of a backend route.</summary>
public readonly record struct ArtifactDirectoryId
{
    /// <summary>Initializes a non-blank logical directory.</summary>
    /// <param name="value">The stable logical value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactDirectoryId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    /// <summary>Gets the stable logical value.</summary>
    public string Value { get; }
    /// <summary>Returns the stable logical value.</summary>
    public override string ToString() => Value;
}
