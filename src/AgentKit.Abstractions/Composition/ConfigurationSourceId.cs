// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one configuration publisher with nonblank ordinal text.</summary>
/// <remarks>The default value is uninitialized and formats as an empty string.</remarks>
public readonly record struct ConfigurationSourceId
{
    /// <summary>Initializes a source identity.</summary>
    /// <param name="value">Nonblank ordinal source text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ConfigurationSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets source identity text.</summary>
    /// <value>Nonblank ordinal text, or null only on an uninitialized default.</value>
    public string? Value { get; }

    /// <summary>Formats the source identity.</summary>
    /// <returns>The source text, or an empty string for an uninitialized default.</returns>
    public override string ToString() => Value ?? string.Empty;
}
