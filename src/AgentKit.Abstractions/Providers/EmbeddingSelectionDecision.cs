// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The chosen embedding model and reproducibility evidence.</summary>
public sealed record EmbeddingSelectionDecision
{
    /// <summary>Initializes a selection decision.</summary>
    /// <param name="model">The chosen descriptor.</param>
    /// <param name="reason">A redacted explanation of the choice.</param>
    /// <param name="catalogVersion">The catalog revision used for selection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null or whitespace.</exception>
    public EmbeddingSelectionDecision(
        EmbeddingModelDescriptor model,
        string reason,
        ModelCatalogVersion catalogVersion)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Model = model;
        Reason = reason;
        CatalogVersion = catalogVersion;
    }

    /// <summary>Gets the chosen descriptor.</summary>
    public EmbeddingModelDescriptor Model { get; init; }

    /// <summary>Gets the redacted explanation.</summary>
    public string Reason { get; init; }

    /// <summary>Gets the catalog revision.</summary>
    public ModelCatalogVersion CatalogVersion { get; init; }
}
