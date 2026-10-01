// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Is one test caller: an agent, session, run, tenant identity, captured authorization, and the memory operation context built from them.</summary>
/// <param name="AgentId">The owning agent.</param>
/// <param name="SessionId">The owning session.</param>
/// <param name="RunId">The run the operation belongs to.</param>
/// <param name="Identity">The authenticated identity.</param>
/// <param name="Authorization">The captured authorization whose scope is exactly this owner.</param>
/// <param name="Context">The memory operation context that agrees with the authorization.</param>
public sealed record MemoryTestOwner(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization,
    MemoryOperationContext Context);
