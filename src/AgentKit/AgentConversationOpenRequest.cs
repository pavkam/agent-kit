// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Names one agent, identity, and optional existing session when opening a conversation through
/// <see cref="AgentEngine.OpenSessionAsync"/>.
/// </summary>
/// <param name="AgentId">The hosted agent the conversation runs.</param>
/// <param name="Identity">The already-authenticated owner of the session.</param>
/// <param name="SessionId">The session to resume, or <see langword="null"/> to defer creation until the first turn.</param>
public sealed record AgentConversationOpenRequest(
    AgentId AgentId,
    ExecutionIdentity Identity,
    SessionId? SessionId = null);
