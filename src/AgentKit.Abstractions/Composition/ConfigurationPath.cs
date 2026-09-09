// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one nonblank semantic configuration path.</summary>
/// <remarks>The compiler validates declared namespace and setting grammar. The default value is uninitialized and formats as empty.</remarks>
public readonly record struct ConfigurationPath
{
    /// <summary>Initializes a semantic path.</summary>
    /// <param name="value">Nonblank ordinal path text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ConfigurationPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets path text.</summary>
    /// <value>Nonblank ordinal text, or null only on an uninitialized default.</value>
    public string? Value { get; }

    /// <summary>Formats the path.</summary>
    /// <returns>The path text, or an empty string for an uninitialized default.</returns>
    public override string ToString() => Value ?? string.Empty;
}
