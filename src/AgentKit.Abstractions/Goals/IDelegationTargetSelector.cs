// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Chooses exactly one target from a captured catalog for one request.</summary>
/// <remarks>Selection never broadens the request and never reorders the catalog. Unresolved ambiguity is a rejection, never a silent pick.</remarks>
public interface IDelegationTargetSelector
{
    /// <summary>Selects the target a request names from a captured snapshot.</summary>
    /// <param name="request">The delegation request.</param>
    /// <param name="snapshot">The captured catalog.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The chosen target, or a rejection for an unknown or ambiguous target.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationTargetSelectionResult> SelectAsync(
        DelegationRequest request,
        DelegationTargetCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
