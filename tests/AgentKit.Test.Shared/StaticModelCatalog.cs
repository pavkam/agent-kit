// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IModelCatalog"/> test double that returns one fixed snapshot and counts reads.</summary>
/// <param name="snapshot">The snapshot every read returns.</param>
public sealed class StaticModelCatalog(ModelCatalogSnapshot snapshot): IModelCatalog
{
    /// <summary>Gets the number of times the catalog was read.</summary>
    public int ReadCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        return ValueTask.FromResult(snapshot);
    }
}
