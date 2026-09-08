// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the terminal result of looking up a session-creation idempotency route.</summary>
/// <remarks>Creation lookup occurs before a session identity is allocated. Its missing result therefore carries no fabricated session address.</remarks>
public abstract record SessionCreationLocationResult;
