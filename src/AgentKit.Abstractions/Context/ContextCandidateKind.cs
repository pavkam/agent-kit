// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies how a context candidate may be projected and interpreted.</summary>
/// <remarks>The kind governs projection and instruction authority; it does not describe where content originated.</remarks>
public enum ContextCandidateKind
{
    /// <summary>Content eligible for instruction projection under its separately retained trust evidence.</summary>
    Instruction,
    /// <summary>Reference material that must remain data rather than instruction authority.</summary>
    ReferenceData,
    /// <summary>Runtime observations that must remain data rather than instruction authority.</summary>
    RuntimeData,
}
