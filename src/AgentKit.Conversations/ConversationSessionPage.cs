// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Returns one ordered page of visible conversation sessions.</summary>
public sealed record ConversationSessionPage(
    ImmutableArray<ConversationSessionSummary> Sessions,
    SessionId? NextCursor): ConversationSessionListResult;
