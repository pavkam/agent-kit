// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Names the finite terminal outcomes of one durable-journal evidence load.</summary>
internal enum DurableJournalEvidenceOutcome
{
    /// <summary>Complete durable evidence was assembled and returned.</summary>
    Loaded,

    /// <summary>No record exists for the requested operation address.</summary>
    NotFound,

    /// <summary>The caller cancelled the attempt before a terminal outcome.</summary>
    Cancelled,
}
