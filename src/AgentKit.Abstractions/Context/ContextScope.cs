// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the narrowest applicability boundary of a context candidate.</summary>
/// <remarks>A scope carries neither domain identities nor authority. Coordinates remain separate captured evidence.</remarks>
public enum ContextScope
{
    /// <summary>The candidate applies to the composed engine.</summary>
    Engine,
    /// <summary>The candidate applies to one agent definition.</summary>
    Agent,
    /// <summary>The candidate applies to one durable session.</summary>
    Session,
    /// <summary>The candidate applies to one conversation.</summary>
    Conversation,
    /// <summary>The candidate applies to one run.</summary>
    Run,
    /// <summary>The candidate applies to one turn.</summary>
    Turn,
    /// <summary>The candidate applies only to one model request.</summary>
    ModelRequest,
}
