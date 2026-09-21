// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Rebinds message session coordinates to one exact history cursor.</summary>
internal static class HistoryMessageCoordinateAlignment
{
    /// <summary>Aligns every message to the cursor's agent, session, conversation, and branch coordinates.</summary>
    /// <param name="messages">The repaired messages to align.</param>
    /// <param name="cursor">The source cursor every message must match.</param>
    /// <returns>Messages rebound to the cursor coordinates.</returns>
    internal static ImmutableArray<AgentMessage> Align(ImmutableArray<AgentMessage> messages, MessageCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        if (messages.IsDefaultOrEmpty)
        {
            return [];
        }

        if (MessagesMatchCursor(messages, cursor))
        {
            return messages;
        }

        var builder = ImmutableArray.CreateBuilder<AgentMessage>(messages.Length);
        foreach (var message in messages)
        {
            builder.Add(Rebind(message, cursor));
        }

        return builder.ToImmutable();
    }

    private static bool MessagesMatchCursor(ImmutableArray<AgentMessage> messages, MessageCursor cursor)
    {
        foreach (var message in messages)
        {
            if (message.AgentId != cursor.AgentId
                || message.SessionId != cursor.SessionId
                || message.ConversationId != cursor.ConversationId
                || message.BranchId != cursor.BranchId)
            {
                return false;
            }
        }

        return true;
    }

    private static AgentMessage Rebind(AgentMessage message, MessageCursor cursor) =>
        message switch
        {
            UserMessage user => new UserMessage(
                user.Id,
                cursor.AgentId,
                cursor.SessionId,
                cursor.ConversationId,
                cursor.BranchId,
                user.RunId,
                user.TurnId,
                user.CreatedAt,
                user.State,
                user.Parts,
                user.Extensions),
            AssistantMessage assistant => new AssistantMessage(
                assistant.Id,
                cursor.AgentId,
                cursor.SessionId,
                cursor.ConversationId,
                cursor.BranchId,
                assistant.RunId,
                assistant.TurnId,
                assistant.CreatedAt,
                assistant.State,
                assistant.Parts,
                assistant.Response,
                assistant.Extensions),
            ToolMessage tool => new ToolMessage(
                tool.Id,
                cursor.AgentId,
                cursor.SessionId,
                cursor.ConversationId,
                cursor.BranchId,
                tool.RunId,
                tool.TurnId,
                tool.CreatedAt,
                tool.State,
                tool.Parts,
                tool.Extensions),
            SystemMessage system => new SystemMessage(
                system.Id,
                cursor.AgentId,
                cursor.SessionId,
                cursor.ConversationId,
                cursor.BranchId,
                system.RunId,
                system.TurnId,
                system.CreatedAt,
                system.State,
                system.Parts,
                system.Extensions),
            DeveloperMessage developer => new DeveloperMessage(
                developer.Id,
                cursor.AgentId,
                cursor.SessionId,
                cursor.ConversationId,
                cursor.BranchId,
                developer.RunId,
                developer.TurnId,
                developer.CreatedAt,
                developer.State,
                developer.Parts,
                developer.Extensions),
            RuntimeMessage runtime => new RuntimeMessage(
                runtime.Id,
                cursor.AgentId,
                cursor.SessionId,
                cursor.ConversationId,
                cursor.BranchId,
                runtime.RunId,
                runtime.TurnId,
                runtime.CreatedAt,
                runtime.State,
                runtime.Parts,
                runtime.Extensions),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, "Unsupported message type."),
        };
}
