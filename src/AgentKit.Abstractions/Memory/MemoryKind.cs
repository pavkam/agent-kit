// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Distinguishes the semantic role of a proposed or durable memory record.</summary>
public enum MemoryKind
{
    /// <summary>A verified factual statement.</summary>
    VerifiedFact,

    /// <summary>A stated user preference.</summary>
    UserPreference,

    /// <summary>An instruction or directive attributed to a user or host.</summary>
    Instruction,

    /// <summary>A recorded decision or commitment.</summary>
    Decision,

    /// <summary>A synthesized summary of prior context.</summary>
    Summary,

    /// <summary>An uncertain or unverified claim that must not be stored as fact.</summary>
    UncertainClaim,
}
