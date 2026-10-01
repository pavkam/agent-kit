// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one configured artifact backend so a profile's logical directory can route to it.</summary>
/// <remarks>The key belongs to the composition root and the artifact runtime. It never appears in a portable write request, reference, session record, message, or provider payload.</remarks>
public readonly record struct ArtifactBackendKey
{
    /// <summary>Initializes a non-blank backend key.</summary>
    /// <param name="value">The stable backend value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ArtifactBackendKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable backend value.</summary>
    public string Value { get; }

    /// <summary>Returns the stable backend value.</summary>
    public override string ToString() => Value;
}
