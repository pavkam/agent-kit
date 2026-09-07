// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the run, session, or other domain owner responsible for artifact retention.</summary>
public readonly record struct ArtifactOwnerId
{
    /// <summary>Initializes a non-blank owner identity.</summary>
    /// <param name="value">The stable owner value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactOwnerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable owner value.</summary>
    public string Value { get; }

    /// <summary>Returns the stable owner value.</summary>
    public override string ToString() => Value;
}
