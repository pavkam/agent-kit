// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract outcome of selecting one delegation target.</summary>
public abstract record DelegationTargetSelectionResult
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected DelegationTargetSelectionResult()
    {
    }
}
