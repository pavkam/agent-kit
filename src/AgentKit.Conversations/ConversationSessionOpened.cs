// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports that an existing authorized session is now bound to the conversation.</summary>
public sealed record ConversationSessionOpened(SessionId SessionId, BranchId BranchId): ConversationSessionOpenResult;
