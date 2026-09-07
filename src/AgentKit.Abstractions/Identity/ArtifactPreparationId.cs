// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one bounded, unpublished artifact staging operation.</summary>
public readonly record struct ArtifactPreparationId
{
    /// <summary>Initializes a non-empty preparation identity.</summary>
    /// <param name="value">The globally unique staging identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public ArtifactPreparationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique staging identity.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form.</summary>
    public override string ToString() => Value.ToString("D");
}
