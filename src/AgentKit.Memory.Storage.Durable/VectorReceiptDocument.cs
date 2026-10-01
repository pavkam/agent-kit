// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="VectorReceipt"/>.</summary>
/// <param name="Tenant">The tenant partition text.</param>
/// <param name="Key">The batch key.</param>
/// <param name="Fingerprint">The batch fingerprint.</param>
/// <param name="Count">The vectors stored or removed.</param>
/// <param name="Watermark">The watermark after the batch.</param>
/// <param name="IsDelete">Whether the batch was a deletion.</param>
internal sealed record VectorReceiptDocument(string Tenant, string Key, string Fingerprint, int Count, long Watermark, bool IsDelete)
{
    /// <summary>Converts a receipt to its persisted form.</summary>
    /// <param name="value">The non-null receipt.</param>
    /// <returns>The document.</returns>
    internal static VectorReceiptDocument FromDomain(VectorReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Tenant.Value, value.Key, value.Fingerprint, value.Count, value.Watermark, value.IsDelete);
    }

    /// <summary>Restores the receipt, re-running validation.</summary>
    /// <returns>The receipt.</returns>
    internal VectorReceipt ToDomain()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(Fingerprint);
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        ArgumentOutOfRangeException.ThrowIfNegative(Watermark);
        return new(new TenantId(Tenant), Key, Fingerprint, Count, Watermark, IsDelete);
    }
}
