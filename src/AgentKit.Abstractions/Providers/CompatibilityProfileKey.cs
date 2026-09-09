// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one published portable compatibility profile.</summary>
/// <remarks>This nonblank ordinal key is selection evidence only; it performs no lookup, wire-profile activation, or authority grant.</remarks>
public readonly record struct CompatibilityProfileKey
{
    /// <summary>Initializes exact profile-key text.</summary>
    /// <param name="value">Nonblank ordinal text preserved without normalization.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public CompatibilityProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact profile-key text.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the key without culture-sensitive conversion.</summary>
    /// <returns>The exact text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
