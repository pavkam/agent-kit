// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one embedding selection decision request.</summary>
public sealed record EmbeddingSelectionRequest
{
    /// <summary>Initializes a selection request.</summary>
    /// <param name="operation">The protected operation requesting selection.</param>
    /// <param name="policy">The configured candidate policy.</param>
    /// <param name="requirements">The portable requirements to satisfy.</param>
    /// <param name="catalog">The catalog snapshot to select from.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public EmbeddingSelectionRequest(
        ProtectedSemanticOperationContext operation,
        EmbeddingSelectionPolicy policy,
        EmbeddingRequirements requirements,
        ModelCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(catalog);
        Operation = operation;
        Policy = policy;
        Requirements = requirements;
        Catalog = catalog;
    }

    /// <summary>Gets the protected operation.</summary>
    public ProtectedSemanticOperationContext Operation { get; }

    /// <summary>Gets the selection policy.</summary>
    public EmbeddingSelectionPolicy Policy { get; }

    /// <summary>Gets the requirements.</summary>
    public EmbeddingRequirements Requirements { get; }

    /// <summary>Gets the catalog snapshot.</summary>
    public ModelCatalogSnapshot Catalog { get; }
}
