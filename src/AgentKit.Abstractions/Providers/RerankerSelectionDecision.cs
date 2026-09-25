// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The chosen reranker and reproducibility evidence.</summary>
public sealed record RerankerSelectionDecision
{
    /// <summary>Initializes a reranker selection decision.</summary>
    /// <param name="model">The chosen descriptor.</param>
    /// <param name="reason">A redacted explanation.</param>
    /// <param name="catalogVersion">The catalog revision used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is invalid.</exception>
    public RerankerSelectionDecision(
        RerankerDescriptor model,
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
    public RerankerDescriptor Model { get; init; }

    /// <summary>Gets the redacted explanation.</summary>
    public string Reason { get; init; }

    /// <summary>Gets the catalog revision.</summary>
    public ModelCatalogVersion CatalogVersion { get; init; }
}
