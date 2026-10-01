// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one configured goal store to a captured goal profile.</summary>
/// <remarks>Selection is by the profile's store key, never by registration order. Implementations are thread-safe and return borrowed instances.</remarks>
public interface IGoalStoreSelector
{
    /// <summary>Selects the store a captured profile names.</summary>
    /// <param name="request">The captured profile whose store is wanted.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The selected store, or a rejection when the profile or store is unknown.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalStoreSelectionResult> SelectAsync(GoalStoreSelectionRequest request, CancellationToken cancellationToken = default);
}
