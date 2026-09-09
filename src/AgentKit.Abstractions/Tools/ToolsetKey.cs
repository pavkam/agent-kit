// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;

/// <summary>Names one authored toolset publication using nonblank ordinal text.</summary>
/// <remarks>The key has no ambient registry lookup and the default value remains an invalid selection sentinel.</remarks>
public readonly record struct ToolsetKey
{
    /// <summary>Creates an exact toolset key without case folding or whitespace normalization.</summary>
    /// <param name="value">The nonblank ordinal key text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolsetKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    /// <summary>Gets the exact authored key text.</summary>
    /// <value>Nonblank ordinal text, or null only on the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact key without culture-sensitive conversion.</summary>
    /// <returns>The authored text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
