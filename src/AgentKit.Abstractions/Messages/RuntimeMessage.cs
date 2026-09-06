// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A synthetic, framework-generated in-run notice, such as an explicit
/// interruption record.
/// </summary>
/// <remarks>
/// The committing component always populates <see cref="AgentMessage.RunId"/>
/// and <see cref="AgentMessage.TurnId"/> for this kind because a runtime
/// notice only ever occurs while a run's turn is active. Compaction and
/// configuration-change records remain distinct session entries; they are
/// never represented as a <see cref="RuntimeMessage"/>.
/// </remarks>
public sealed record RuntimeMessage: AgentMessage
{
    /// <summary>Initializes a new instance of the <see cref="RuntimeMessage"/> record.</summary>
    /// <inheritdoc cref="AgentMessage(MessageId, AgentId, SessionId, ConversationId?, BranchId, RunId?, TurnId?, DateTimeOffset, MessageState, ImmutableArray{ContentPart}, ExtensionData)" path="/param"/>
    public RuntimeMessage(
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
