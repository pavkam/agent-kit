// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Exposes the immutable composition evidence of every keyed artifact coordinator for validation and diagnostics.</summary>
/// <remarks>The catalog is singleton and thread-safe over immutable snapshots. It never resolves a store or performs I/O.</remarks>
public interface IArtifactCoordinatorCatalog
{
    /// <summary>Finds the composition evidence for one coordinator key.</summary>
    /// <param name="key">The coordinator key an agent definition selects.</param>
    /// <param name="snapshot">The immutable evidence when the key was registered through the first-party runtime.</param>
    /// <returns><see langword="true"/> when the key has evidence; <see langword="false"/> for an unknown key or a coordinator replaced by an application type.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    public bool TryGet(ComponentKey<IArtifactCoordinator> key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ArtifactCoordinatorSnapshot? snapshot);
}
