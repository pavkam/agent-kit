// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Provides bounded discovery metadata for one visible session route.</summary>
public sealed record ConversationSessionSummary(
    SessionId SessionId,
    SessionStoreKey StoreKey,
    DateTimeOffset RecordedAt);
