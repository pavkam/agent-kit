// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Remembers one applied batch so an equivalent replay returns the original outcome.</summary>
/// <param name="Tenant">The tenant partition.</param>
/// <param name="Key">The batch's idempotency key.</param>
/// <param name="Fingerprint">The canonical fingerprint of the batch.</param>
/// <param name="Count">The number of vectors the batch stored or removed.</param>
/// <param name="Watermark">The index watermark after the batch.</param>
/// <param name="IsDelete"><see langword="true"/> for a deletion batch, <see langword="false"/> for an upsert.</param>
internal sealed record VectorReceipt(TenantId Tenant, string Key, string Fingerprint, int Count, long Watermark, bool IsDelete);
