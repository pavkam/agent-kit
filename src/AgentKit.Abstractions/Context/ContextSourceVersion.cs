// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies an exact published context-source revision using ordinal text.</summary>
/// <remarks>The version is provenance evidence and does not imply ordering or current availability.</remarks>
public readonly record struct ContextSourceVersion
{
    /// <summary>Creates an exact source version without case folding or normalization.</summary>
    /// <param name="value">The nonblank ordinal version text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public ContextSourceVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact source-version text supplied by the publisher.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact version without culture-sensitive conversion.</summary>
    /// <returns>The version text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
