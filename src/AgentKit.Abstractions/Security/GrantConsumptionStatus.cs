// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the terminal result of validating and consuming a security grant.</summary>
public enum GrantConsumptionStatus
{
    /// <summary>The grant matched and one use was consumed atomically.</summary>
    Consumed,
    /// <summary>The grant was not registered with the authoritative store.</summary>
    Unknown,
    /// <summary>The presented grant differs from the registered immutable evidence.</summary>
    Tampered,
    /// <summary>The concrete enforcement evidence did not exactly match the grant.</summary>
    Mismatch,
    /// <summary>The current time is outside the grant validity window.</summary>
    Expired,
    /// <summary>The grant was revoked or its revocation epoch is stale.</summary>
    Revoked,
    /// <summary>Every allowed use was already consumed.</summary>
    Exhausted,
    /// <summary>The intent identity already consumed a use; the historical receipt is returned only for reconciliation and grants no permission to repeat the effect.</summary>
    Reconciled,
}
