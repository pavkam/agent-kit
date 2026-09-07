// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the typed terminal outcome of one delegation request.</summary>
public abstract record TaskDelegationResult
{
    private protected TaskDelegationResult(DelegationId id) => Id = id;

    /// <summary>Gets the correlated delegation identity.</summary>
    public DelegationId Id { get; init; }
}
