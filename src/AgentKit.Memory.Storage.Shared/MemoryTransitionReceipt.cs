// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Remembers one applied lifecycle transition so an equivalent replay returns the original outcome.</summary>
/// <param name="Key">The transition's idempotency key.</param>
/// <param name="To">The target state the transition requested.</param>
/// <param name="Expected">The version the transition expected.</param>
/// <param name="ReplacementId">The replacement identity of a correction, or <see langword="null"/>.</param>
/// <param name="Result">The record after the transition.</param>
/// <param name="Replacement">The replacement record a correction created, or <see langword="null"/>.</param>
internal sealed record MemoryTransitionReceipt(
    string Key,
    MemoryLifecycleState To,
    VersionToken Expected,
    MemoryId? ReplacementId,
    DurableMemoryRecord Result,
    DurableMemoryRecord? Replacement);
