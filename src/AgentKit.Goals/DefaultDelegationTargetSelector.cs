// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Chooses the one target a request names, rejecting unknown or ambiguous targets.</summary>
/// <remarks>A target published identically by more than one provider is not ambiguous; two different publications of one agent are, and selection never picks between them.</remarks>
internal sealed class DefaultDelegationTargetSelector: IDelegationTargetSelector
{
    /// <inheritdoc/>
    public ValueTask<DelegationTargetSelectionResult> SelectAsync(
        DelegationRequest request,
        DelegationTargetCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = snapshot.Targets.Where(target => target.AgentId == request.TargetAgentId).Distinct().ToList();
        DelegationTargetSelectionResult result = matches.Count switch
        {
            0 => new DelegationTargetRejected(new DelegationRejection(DelegationRejectionKind.UnknownTarget, "The target agent is not a discoverable delegation target.")),
            1 => new DelegationTargetSelected(matches[0]),
            _ => new DelegationTargetRejected(new DelegationRejection(DelegationRejectionKind.AmbiguousTarget, "More than one discoverable publication names the target agent.")),
        };
        return ValueTask.FromResult(result);
    }
}
