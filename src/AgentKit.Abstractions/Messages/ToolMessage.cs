// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A message carrying the committed <see cref="ToolResultPart"/> values for
/// one or more accepted tool calls.
/// </summary>
/// <remarks>
/// The committing component always populates <see cref="AgentMessage.RunId"/>
/// and <see cref="AgentMessage.TurnId"/> for this kind because a tool result
/// only ever occurs while a run's turn is active.
/// </remarks>
public sealed record ToolMessage: AgentMessage
{
    /// <summary>Initializes a new instance of the <see cref="ToolMessage"/> record.</summary>
    /// <inheritdoc cref="AgentMessage(MessageId, AgentId, SessionId, ConversationId?, BranchId, RunId?, TurnId?, DateTimeOffset, MessageState, ImmutableArray{ContentPart}, ExtensionData)" path="/param"/>
    public ToolMessage(
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
