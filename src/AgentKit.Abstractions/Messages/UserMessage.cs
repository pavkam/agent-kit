// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A message representing input attributed to the end user or calling
/// application on behalf of the user.
/// </summary>
/// <remarks>
/// The committing component always populates <see cref="AgentMessage.RunId"/>
/// and <see cref="AgentMessage.TurnId"/> for this kind because user input is
/// only ever recorded while promoting admitted input into a run's turn.
/// </remarks>
public sealed record UserMessage: AgentMessage
{
    /// <summary>Initializes a new instance of the <see cref="UserMessage"/> record.</summary>
    /// <inheritdoc cref="AgentMessage(MessageId, AgentId, SessionId, ConversationId?, BranchId, RunId?, TurnId?, DateTimeOffset, MessageState, ImmutableArray{ContentPart}, ExtensionData)" path="/param"/>
    public UserMessage(
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
