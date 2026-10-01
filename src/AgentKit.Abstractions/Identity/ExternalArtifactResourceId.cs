// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the resource an external system owns, independently of any unsigned locator or signed URL.</summary>
public readonly record struct ExternalArtifactResourceId
{
    /// <summary>Initializes a non-blank external resource identity.</summary>
    /// <param name="value">The stable external resource value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ExternalArtifactResourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable external resource value.</summary>
    public string Value { get; }

    /// <summary>Returns the stable external resource value.</summary>
    public override string ToString() => Value;
}
