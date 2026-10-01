// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the one target a selector chose.</summary>
public sealed record DelegationTargetSelected: DelegationTargetSelectionResult
{
    /// <summary>Initializes a selected result.</summary>
    /// <param name="target">The chosen target.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    public DelegationTargetSelected(DelegationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
    }

    /// <summary>Gets the chosen target.</summary>
    public DelegationTarget Target { get; }
}
