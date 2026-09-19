// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Names one immutable capture generation of a hook catalog snapshot for a
/// hook profile, distinguishing it from an earlier or later capture of the
/// same profile taken at a different reload boundary.
/// </summary>
/// <remarks>
/// This immutable value uses ordinal text equality and names a capture
/// generation only; it does not convey ordering, authority, or content. A
/// default instance has no usable version text; consumers must reject it at
/// their boundary.
/// </remarks>
public readonly record struct HookCatalogVersion
{
    /// <summary>Initializes a validated hook-catalog capture-generation identity.</summary>
    /// <param name="value">The non-blank canonical version text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of whitespace.</exception>
    public HookCatalogVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical version text.</summary>
    /// <value>The exact caller-supplied text. A default instance exposes <see langword="null"/> at runtime.</value>
    public string Value { get; }

    /// <summary>Returns the canonical version text for diagnostics.</summary>
    /// <returns>The exact version text, or <see cref="string.Empty"/> for a default instance.</returns>
    public override string ToString() => Value ?? string.Empty;
}
