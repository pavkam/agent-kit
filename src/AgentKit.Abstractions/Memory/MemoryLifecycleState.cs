// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the durable lifecycle state of one memory record.</summary>
public enum MemoryLifecycleState
{
    /// <summary>The record is proposed but not yet accepted as durable state.</summary>
    Proposed,

    /// <summary>The record passed validation and policy review.</summary>
    Validated,

    /// <summary>The record was accepted and is eligible to become active.</summary>
    Accepted,

    /// <summary>The record is active for retrieval subject to visibility and policy.</summary>
    Active,

    /// <summary>The record was rejected and must not become active.</summary>
    Rejected,

    /// <summary>The record was superseded or corrected while retaining history.</summary>
    Corrected,

    /// <summary>The record is logically deleted and invisible to new retrieval.</summary>
    Deleted,

    /// <summary>The record expired under its retention policy.</summary>
    Expired,
}
