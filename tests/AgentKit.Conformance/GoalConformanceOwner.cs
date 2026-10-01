// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Identifies the agent, session, run, and authority that own goals in one goal-store conformance case.</summary>
/// <param name="AgentId">The owning agent.</param>
/// <param name="SessionId">The owning session, which the adapter may need to exist.</param>
/// <param name="RunId">The owning run.</param>
/// <param name="Identity">The authenticated identity whose tenant partitions the goals.</param>
/// <param name="Authorization">The captured authorization whose scope is exactly this owner.</param>
public sealed record GoalConformanceOwner(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization);
