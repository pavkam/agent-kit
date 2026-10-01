// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names which kind of component requested a goal transition.</summary>
/// <remarks>The actor is recorded evidence. It never grants authority; the security grant consumed by the store does.</remarks>
public enum TransitionActor
{
    /// <summary>The goal's owning agent, acting inside its own run.</summary>
    Agent = 0,

    /// <summary>The goal or delegation coordinator applying lifecycle policy.</summary>
    Coordinator = 1,

    /// <summary>A host-owned worker draining durable intents.</summary>
    Worker = 2,

    /// <summary>A human or application operator acting outside any agent run.</summary>
    Operator = 3,

    /// <summary>The framework itself, for example when a deadline elapses.</summary>
    System = 4,
}
