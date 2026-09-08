// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies stable idempotency and any required ownership fence for one exact grant-consumption attempt.</summary>
public sealed record SecurityEnforcementIntent
{
    /// <summary>Initializes an enforcement intent.</summary><param name="id">The non-default stable attempt identity.</param><param name="requiredFence">The exact distributed ownership fence, or null for an explicitly local effect.</param><exception cref="ArgumentOutOfRangeException">A supplied identity or fence is default.</exception>
    public SecurityEnforcementIntent(SecurityEnforcementIntentId id, FencingToken? requiredFence)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        if (requiredFence is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(requiredFence));
        }
        Id = id;
        RequiredFence = requiredFence;
    }

    /// <summary>Gets the stable attempt identity.</summary><value>The non-default identity used for exact consumption reconciliation.</value>
    public SecurityEnforcementIntentId Id { get; }
    /// <summary>Gets the required ownership fence.</summary><value>The exact distributed fence or null when the protected effect is process-local.</value>
    public FencingToken? RequiredFence { get; }
}
