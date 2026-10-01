// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one configured dispatcher to a captured goal profile.</summary>
/// <remarks>Selection is by the profile's dispatcher key, never by registration order.</remarks>
public interface IDelegationDispatcherSelector
{
    /// <summary>Selects the dispatcher a captured profile names.</summary>
    /// <param name="request">The captured profile whose dispatcher is wanted.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The selected dispatcher, or a rejection when the profile or dispatcher is unknown.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationDispatcherSelectionResult> SelectAsync(
        DelegationDispatcherSelectionRequest request,
        CancellationToken cancellationToken = default);
}
