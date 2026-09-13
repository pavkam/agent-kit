// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Names the finite terminal outcomes of one execution-lease renewal attempt.</summary>
internal enum DurableLeaseRenewalOutcome
{
    /// <summary>The presented generation is still authoritative, so expiry was extended.</summary>
    Renewed,

    /// <summary>The presented generation is no longer authoritative.</summary>
    Lost,

    /// <summary>The caller cancelled the attempt before a terminal outcome.</summary>
    Cancelled,

    /// <summary>Renewal ended with an unexpected failure.</summary>
    Failed,
}
