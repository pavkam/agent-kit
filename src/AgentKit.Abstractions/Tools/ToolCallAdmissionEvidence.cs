// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures bounded raw call admission evidence without claiming semantic argument validation.</summary>
public sealed record ToolCallAdmissionEvidence
{
    /// <summary>Initializes immutable raw admission evidence without asserting that arguments were valid.</summary>
    /// <param name="catalogVersion">The exact nondefault catalog revision used for admission.</param>
    /// <param name="sourceOrdinal">The nonnegative ordinal of the provider-emitted call.</param>
    /// <param name="rawArgumentsFingerprint">The nondefault fingerprint of the bounded raw arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="catalogVersion"/> or <paramref name="rawArgumentsFingerprint"/> is default, or <paramref name="sourceOrdinal"/> is negative.</exception>
    public ToolCallAdmissionEvidence(ToolCatalogVersion catalogVersion, int sourceOrdinal, InputFingerprint rawArgumentsFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(catalogVersion, default);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOrdinal);
        ArgumentOutOfRangeException.ThrowIfEqual(rawArgumentsFingerprint, default);
        CatalogVersion = catalogVersion;
        SourceOrdinal = sourceOrdinal;
        RawArgumentsFingerprint = rawArgumentsFingerprint;
    }
    /// <summary>Gets the captured catalog revision.</summary>
    /// <value>Revision at admission.</value>
    public ToolCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the provider-source ordinal.</summary>
    /// <value>A nonnegative stable ordinal.</value>
    public int SourceOrdinal { get; }

    /// <summary>Gets the raw bounded-argument fingerprint.</summary>
    /// <value>Evidence only, not validation proof.</value>
    public InputFingerprint RawArgumentsFingerprint { get; }
}
