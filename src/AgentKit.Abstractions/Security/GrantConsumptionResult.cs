// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports whether a bounded grant was validated and consumed before an effect.</summary>
public sealed record GrantConsumptionResult
{
    /// <summary>Initializes a grant-consumption result.</summary>
    /// <param name="status">The terminal classification.</param>
    /// <param name="remainingUses">Uses remaining after this attempt; zero for unknown evidence.</param>
    /// <param name="safeMessage">A non-sensitive explanation suitable for callers and audit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined or <paramref name="remainingUses"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public GrantConsumptionResult(GrantConsumptionStatus status, int remainingUses, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingUses);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Status = status;
        RemainingUses = remainingUses;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal classification.</summary>
    public GrantConsumptionStatus Status { get; init; }
    /// <summary>Gets the authoritative remaining use count.</summary>
    public int RemainingUses { get; init; }
    /// <summary>Gets a non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
