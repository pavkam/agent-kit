// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Owns synchronization and exact owner evidence for one process-local session execution lane.</summary>
internal sealed class SessionRunSlot
{
    /// <summary>Gets the binary gate serializing lane owners and waiters.</summary>
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Gets the monitor protecting <see cref="Owner"/> and gate release linearization.</summary>
    internal object SyncRoot { get; } = new();

    /// <summary>Gets or sets the exact provisional or acquired owner while holding <see cref="SyncRoot"/>.</summary>
    internal SessionRunSlotOwner? Owner { get; set; }
}
