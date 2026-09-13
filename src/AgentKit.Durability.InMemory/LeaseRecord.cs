// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Stores one durable operation's current ownership generation under the manager's single gate.</summary>
/// <remarks>All access to instances of this type is serialized by <see cref="InMemoryDurableLeaseManager"/>'s single gate; this class performs no synchronization of its own.</remarks>
internal sealed class LeaseRecord
{
    /// <summary>Initializes one granted ownership generation.</summary>
    /// <param name="ownerWorkerId">The worker granted this generation.</param>
    /// <param name="fencingToken">The atomically allocated ownership generation.</param>
    /// <param name="duration">The requested lease length, reused unchanged by every renewal of this generation.</param>
    /// <param name="expiresAt">The instant this generation expires unless renewed.</param>
    internal LeaseRecord(WorkerId ownerWorkerId, FencingToken fencingToken, TimeSpan duration, DateTimeOffset expiresAt)
    {
        OwnerWorkerId = ownerWorkerId;
        FencingToken = fencingToken;
        Duration = duration;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the worker granted this generation.</summary>
    internal WorkerId OwnerWorkerId { get; }

    /// <summary>Gets the atomically allocated ownership generation.</summary>
    internal FencingToken FencingToken { get; }

    /// <summary>Gets the requested lease length reused by every renewal of this generation.</summary>
    internal TimeSpan Duration { get; }

    /// <summary>Gets or sets the instant this generation expires unless renewed.</summary>
    internal DateTimeOffset ExpiresAt { get; set; }
}
