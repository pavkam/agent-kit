// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A message representing developer- or workspace-authored instructions,
/// distinct from framework/host <see cref="SystemMessage"/> content and
/// end-user <see cref="UserMessage"/> content.
/// </summary>
/// <remarks>
/// Developer instructions may be recorded before any run exists, so
/// <see cref="AgentMessage.RunId"/> and <see cref="AgentMessage.TurnId"/>
/// legitimately remain <see langword="null"/> for this kind.
/// </remarks>
public sealed record DeveloperMessage: AgentMessage
{
    /// <summary>Initializes a new instance of the <see cref="DeveloperMessage"/> record.</summary>
    /// <inheritdoc cref="AgentMessage(MessageId, AgentId, SessionId, ConversationId?, BranchId, RunId?, TurnId?, DateTimeOffset, MessageState, ImmutableArray{ContentPart}, ExtensionData)" path="/param"/>
    public DeveloperMessage(
        MessageId id,
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        BranchId branchId,
        RunId? runId,
        TurnId? turnId,
        DateTimeOffset createdAt,
        MessageState state,
        ImmutableArray<ContentPart> parts,
        ExtensionData extensions)
        : base(
            id,
            agentId,
            sessionId,
            conversationId,
            branchId,
            runId,
            turnId,
            createdAt,
            state,
            parts,
            extensions)
    {
    }
}
