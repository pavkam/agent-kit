// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one reranker selection request.</summary>
public sealed record RerankerSelectionRequest
{
    /// <summary>Initializes a reranker selection request.</summary>
    /// <param name="operation">The protected operation.</param>
    /// <param name="policy">The candidate policy.</param>
    /// <param name="requirements">The portable requirements.</param>
    /// <param name="catalog">The catalog snapshot.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public RerankerSelectionRequest(
        ProtectedSemanticOperationContext operation,
        RerankerSelectionPolicy policy,
        RerankerRequirements requirements,
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

    /// <summary>Gets the candidate policy.</summary>
    public RerankerSelectionPolicy Policy { get; }

    /// <summary>Gets the requirements.</summary>
    public RerankerRequirements Requirements { get; }

    /// <summary>Gets the catalog snapshot.</summary>
    public ModelCatalogSnapshot Catalog { get; }
}
