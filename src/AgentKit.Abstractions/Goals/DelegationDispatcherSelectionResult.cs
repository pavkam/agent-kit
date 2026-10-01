// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract outcome of selecting a delegation dispatcher.</summary>
public abstract record DelegationDispatcherSelectionResult
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected DelegationDispatcherSelectionResult()
    {
    }
}
