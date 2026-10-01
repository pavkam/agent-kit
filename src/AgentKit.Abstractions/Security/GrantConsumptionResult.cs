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
    /// <param name="intentReceipt">The atomically retained permission-to-start receipt: required when <paramref name="status"/> is consumed or reconciled, and null for every other status.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined or <paramref name="remainingUses"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank, a consumed or reconciled status carries no <paramref name="intentReceipt"/>, or any other status carries one.</exception>
    public GrantConsumptionResult(GrantConsumptionStatus status, int remainingUses, string safeMessage,
        SecurityEnforcementIntentReceipt? intentReceipt)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingUses);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentException.ThrowIfInvalidIntentReceipt(status, intentReceipt);
        Status = status;
        RemainingUses = remainingUses;
        SafeMessage = safeMessage;
        IntentReceipt = intentReceipt;
    }

    /// <summary>Gets the terminal classification.</summary>
    public GrantConsumptionStatus Status { get; }
    /// <summary>Gets the authoritative remaining use count.</summary>
    public int RemainingUses { get; }
    /// <summary>Gets a non-sensitive explanation.</summary>
    public string SafeMessage { get; }
    /// <summary>Gets the atomically retained intent receipt when intent-aware consumption produced authoritative evidence.</summary><value>A fresh permission-to-start receipt for <see cref="GrantConsumptionStatus.Consumed"/>, historical receipt-only evidence for <see cref="GrantConsumptionStatus.Reconciled"/>, or null for unsuccessful results.</value>
    public SecurityEnforcementIntentReceipt? IntentReceipt { get; }
}
