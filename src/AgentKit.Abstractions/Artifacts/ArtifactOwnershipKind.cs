// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies which domain object owns artifact retention.</summary>
public enum ArtifactOwnershipKind
{
    /// <summary>A single run owns the artifact.</summary>
    Run,
    /// <summary>A durable session owns the artifact.</summary>
    Session,
    /// <summary>A durable memory record owns the artifact.</summary>
    Memory,
    /// <summary>An evaluation owns the artifact.</summary>
    Evaluation,
    /// <summary>An external system owns the content.</summary>
    External,
}
