// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Controls how often the coordinator commits durable checkpoints.</summary>
public enum DurableCheckpointMode
{
    /// <summary>Checkpoint only at semantic boundaries such as model and tool completion.</summary>
    SemanticBoundaries,

    /// <summary>Checkpoint after every journal-eligible transition.</summary>
    EveryTransition,
}
