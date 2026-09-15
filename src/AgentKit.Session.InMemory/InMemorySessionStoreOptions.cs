// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Configures the process-local bounds enforced by <see cref="InMemorySessionStore"/>.</summary>
/// <remarks>
/// <para>
/// This is a mutable options type bound through <see cref="IOptions{TOptions}"/>;
/// it is configured once at composition time and treated as read-only by the
/// store afterward. Changing a property after the store has been constructed
/// has no effect on that instance.
/// </para>
/// <para>
/// The store validates every bound again in its constructor, so a value that
/// escapes options validation still fails at construction rather than during
/// an agent run.
/// </para>
/// </remarks>
public sealed class InMemorySessionStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of distinct store-issued
    /// <see cref="SessionReadSnapshot"/> values the store remembers as
    /// continuation evidence.
    /// </summary>
    /// <value>Defaults to 4,096 and must be positive.</value>
    /// <remarks>
    /// <para>
    /// A paged read that supplies no snapshot receives a fresh snapshot for
    /// the branch's current version and tip. Exact continuation later requires
    /// that same snapshot, and the store only honors snapshots it issued itself
    /// so that a caller cannot fabricate continuation evidence. Each newly
    /// issued distinct snapshot is retained in first-in, first-out order up to
    /// this bound.
    /// </para>
    /// <para>
    /// Once the bound is exceeded the oldest retained snapshot is evicted.
    /// Continuing a read from an evicted snapshot is reported as a typed
    /// <see cref="SessionReadFailed"/> stating that the snapshot is not
    /// available for the branch; the caller must start a new paged read.
    /// Lower values cap the memory held by a long-lived store; higher values
    /// let more concurrent or long-paused paged readers keep exact
    /// continuation.
    /// </para>
    /// </remarks>
    public int MaximumIssuedReadSnapshots { get; set; } = 4096;
}
