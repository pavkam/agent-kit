// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one artifact event sink registration.</summary>
public readonly record struct ArtifactEventSinkId
{
    /// <summary>Initializes a non-blank sink identity.</summary>
    /// <param name="value">The stable sink value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactEventSinkId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable sink value.</summary>
    public string Value { get; }

    /// <summary>Returns the stable sink value.</summary>
    public override string ToString() => Value;
}
