// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The outcome of <see cref="AgentEngine.OpenSessionAsync"/>.</summary>
public abstract record AgentConversationOpenResult;

/// <summary>Reports a validated session binding the caller may continue on the engine surface.</summary>
/// <param name="AgentId">The agent that owns the session.</param>
/// <param name="SessionId">The opened or deferred session identity.</param>
/// <param name="BranchId">The authoritative active branch when <paramref name="SessionId"/> was opened; otherwise default until the first turn.</param>
public sealed record AgentConversationOpened(AgentId AgentId, SessionId SessionId, BranchId BranchId)
    : AgentConversationOpenResult;

/// <summary>Reports that the conversation could not be opened with safe, closed evidence.</summary>
/// <param name="AgentId">The agent the caller requested.</param>
/// <param name="SafeMessage">A bounded reason safe to log or surface.</param>
public sealed record AgentConversationOpenRejected(AgentId AgentId, string SafeMessage)
    : AgentConversationOpenResult;
