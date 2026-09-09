// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable tool-catalog snapshot.</summary>
/// <remarks>Version text is compared ordinally and conveys no ordering or freshness by itself.</remarks>
public readonly record struct ToolCatalogVersion
{
    /// <summary>Initializes a tool-catalog snapshot version.</summary>
    /// <param name="value">The nonblank stable version text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolCatalogVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable catalog-version text.</summary>
    /// <value>Ordinal text retained without normalization.</value>
    public string Value { get; }

    /// <summary>Returns the stable catalog-version text.</summary>
    /// <returns>The value supplied at construction.</returns>
    public override string ToString() => Value;
}
