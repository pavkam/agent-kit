// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Supplies a stable idempotency identity and the ownership generation, when
/// one already exists, for one exact grant-consumption attempt.
/// </summary>
/// <remarks>
/// <see cref="RequiredFence"/> describes the ownership required by the
/// immediate protected action; it does not describe where the effect or its
/// storage runs. A non-null value binds an action performed under an existing
/// distributed owner to that owner's exact generation. A null value says the
/// selected immediate action does not require a generation already acquired by
/// this worker, such as an authorized evidence
/// read whose contract does not require ownership or acquisition that atomically
/// seeks a new generation. It never relaxes exact grant, audience, resource,
/// receipt, or required-audit validation. An effect contract that requires
/// existing ownership must fail closed when this value is absent or does not
/// match.
/// </remarks>
public sealed record SecurityEnforcementIntent
{
    /// <summary>Initializes one exact grant-consumption attempt.</summary>
    /// <param name="id">The non-default stable attempt identity.</param>
    /// <param name="requiredFence">
    /// The exact existing distributed ownership generation required by the
    /// immediate action, or <see langword="null"/> when the selected action does
    /// not require existing ownership.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> is default, or a supplied
    /// <paramref name="requiredFence"/> is a default, unallocated token.
    /// </exception>
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

    /// <summary>Gets the stable attempt identity.</summary>
    /// <value>The non-default identity used for exact consumption reconciliation.</value>
    public SecurityEnforcementIntentId Id { get; }

    /// <summary>Gets the exact existing ownership generation required by this action.</summary>
    /// <value>
    /// The current non-default distributed fence when the action operates under
    /// an acquired owner; otherwise <see langword="null"/> when the selected action
    /// does not require a generation already acquired by this worker. Absence
    /// does not assert local storage or relax any other enforcement requirement.
    /// </value>
    public FencingToken? RequiredFence { get; }
}
