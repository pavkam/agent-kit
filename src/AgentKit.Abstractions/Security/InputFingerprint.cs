// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the exact normalized input bytes authorized for an effect.</summary>
public readonly record struct InputFingerprint
{
    /// <summary>Initializes a validated fingerprint.</summary>
    /// <param name="value">The canonical algorithm-qualified digest text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public InputFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical digest text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical digest text.</summary>
    public override string ToString() => Value;
}
