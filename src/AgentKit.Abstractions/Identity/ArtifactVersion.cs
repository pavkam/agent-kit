// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable published version of a logical artifact.</summary>
public readonly record struct ArtifactVersion
{
    /// <summary>Initializes a non-blank version.</summary>
    /// <param name="value">The stable version value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable version value.</summary>
    public string Value { get; }

    /// <summary>Returns the stable version value.</summary>
    public override string ToString() => Value;
}
