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

    /// <summary>
    /// Gets or sets the number of in-flight <c>AcquireAsync</c> calls currently referencing this exact instance,
    /// while holding <see cref="SyncRoot"/>.
    /// </summary>
    /// <remarks>
    /// This is distinct from <see cref="Owner"/>: an acquirer's reference is held from the moment it looks the
    /// slot up until it either installs itself as <see cref="Owner"/> or returns without acquiring, whereas
    /// <see cref="Owner"/> tracks the outstanding lease itself. A slot may be removed from the owning
    /// dictionary only when both this count and <see cref="Owner"/> indicate nobody is using or about to use it;
    /// checking either alone would let a concurrent lookup race a removal and end up coordinating the same
    /// logical lane through two different instances.
    /// </remarks>
    internal int RefCount { get; set; }

    /// <summary>
    /// Gets or sets whether this instance has already been removed from the owning dictionary, while holding
    /// <see cref="SyncRoot"/>.
    /// </summary>
    /// <remarks>
    /// A caller that finds a retired instance via a dictionary lookup that raced the removal must discard it and
    /// look up (or create) a fresh instance instead of referencing or reviving a retired one.
    /// </remarks>
    internal bool Retired { get; set; }
}
