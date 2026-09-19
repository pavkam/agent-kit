// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the trigger and a non-sensitive explanation for retiring a bounded grant.</summary>
public sealed record RevocationReason
{
    /// <summary>Initializes a revocation reason.</summary>
    /// <param name="trigger">The classification of the retiring event.</param>
    /// <param name="safeMessage">The non-sensitive explanation suitable for audit and callers.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trigger"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public RevocationReason(SecurityRevocationTrigger trigger, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(trigger);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Trigger = trigger;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the retirement trigger.</summary>
    public SecurityRevocationTrigger Trigger { get; }
    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; }
}
