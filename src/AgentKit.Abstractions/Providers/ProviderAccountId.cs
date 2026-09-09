// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one provider account with exact nonblank ordinal text.</summary>
/// <remarks>This local value performs no lookup, publication, validation of an external service, or authority grant. The CLR default is an invalid sentinel.</remarks>
public readonly record struct ProviderAccountId
{
    /// <summary>Initializes the exact authored value.</summary>
    /// <param name="value">Nonblank ordinal text preserved without normalization.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ProviderAccountId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact authored text.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the value without culture-sensitive conversion.</summary>
    /// <returns>The exact text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
