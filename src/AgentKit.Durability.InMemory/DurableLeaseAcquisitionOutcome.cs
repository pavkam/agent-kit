// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Names the finite terminal outcomes of one execution-lease acquisition attempt.</summary>
internal enum DurableLeaseAcquisitionOutcome
{
    /// <summary>No unexpired generation existed, so a new one was granted.</summary>
    GrantedFirstOwnership,

    /// <summary>The previous generation had expired, so a new one was granted by takeover.</summary>
    GrantedByTakeover,

    /// <summary>An unexpired generation is already held, so no new generation was granted.</summary>
    HeldByAnotherWorker,

    /// <summary>The caller cancelled the attempt before a terminal outcome.</summary>
    Cancelled,

    /// <summary>Acquisition ended with an unexpected failure.</summary>
    Failed,
}
