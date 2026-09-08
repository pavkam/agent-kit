// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects a store only from the explicit immutable routing evidence supplied by a coordinator.</summary>
/// <remarks>Implementations map keys against explicitly injected stores. They do not query a directory, scan providers, migrate data, or use an ambient service provider.</remarks>
public interface ISessionStoreSelector
{
    /// <summary>Selects the explicit default store for a new session before its directory route is recorded.</summary>
    /// <param name="request">The immutable new-session selection request.</param>
    /// <param name="cancellationToken">Cancels before selection completes.</param>
    /// <returns>The selected store or a typed rejection with no fallback.</returns>
    public ValueTask<SessionStoreSelectionResult> SelectForCreateAsync(SessionStoreCreateSelectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves an existing session only through the directory's authoritative pinned store key.</summary>
    /// <param name="request">The immutable context, profile, and location evidence.</param>
    /// <param name="cancellationToken">Cancels before selection completes.</param>
    /// <returns>The selected store or a typed rejection with no probing or migration.</returns>
    public ValueTask<SessionStoreSelectionResult> ResolveExistingAsync(SessionStoreSelectionRequest request,
        CancellationToken cancellationToken = default);
}
