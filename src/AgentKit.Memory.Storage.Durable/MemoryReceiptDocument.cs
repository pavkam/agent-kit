// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="MemoryTransitionReceipt"/>.</summary>
/// <param name="Key">The transition key.</param>
/// <param name="To">The requested target state.</param>
/// <param name="Expected">The expected version text.</param>
/// <param name="ReplacementId">The replacement identity, or <see langword="null"/>.</param>
/// <param name="Result">The record after the transition.</param>
/// <param name="Replacement">The replacement record, or <see langword="null"/>.</param>
internal sealed record MemoryReceiptDocument(
    string Key,
    MemoryLifecycleState To,
    string Expected,
    Guid? ReplacementId,
    MemoryRecordDocument Result,
    MemoryRecordDocument? Replacement)
{
    /// <summary>Converts a receipt to its persisted form.</summary>
    /// <param name="value">The non-null receipt.</param>
    /// <returns>The document.</returns>
    internal static MemoryReceiptDocument FromDomain(MemoryTransitionReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Key,
            value.To,
            value.Expected.Value,
            value.ReplacementId?.Value,
            MemoryRecordDocument.FromDomain(value.Result),
            value.Replacement is null ? null : MemoryRecordDocument.FromDomain(value.Replacement));
    }

    /// <summary>Restores the receipt, re-running validation.</summary>
    /// <returns>The receipt.</returns>
    internal MemoryTransitionReceipt ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Result);
        return new(
            Key,
            To,
            new VersionToken(Expected),
            ReplacementId is { } replacement ? new MemoryId(replacement) : null,
            Result.ToDomain(),
            Replacement?.ToDomain());
    }
}
