// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable family of artifact routing and retention policy.</summary>
public readonly record struct ArtifactProfileKey
{
    /// <summary>Initializes a non-blank profile key.</summary>
    /// <param name="value">The stable profile value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    /// <summary>Gets the stable profile value.</summary>
    public string Value { get; }
    /// <summary>Returns the stable profile value.</summary>
    public override string ToString() => Value;
}
