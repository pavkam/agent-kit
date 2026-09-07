// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one logical durable artifact independently of its bytes or backend location.</summary>
public readonly record struct ArtifactId
{
    /// <summary>Initializes a non-empty artifact identity.</summary>
    /// <param name="value">The globally unique logical identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public ArtifactId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique logical identity.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form.</summary>
    public override string ToString() => Value.ToString("D");
}
