// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports one tool call the model requested during a conversational turn.</summary>
/// <remarks>
/// This is a display-oriented projection, not the authoritative call record: it carries the raw requested
/// arguments as text for rendering, without the tool identity, schema, or execution evidence the underlying
/// session and tool-invocation pipeline retain.
/// </remarks>
public sealed record ConversationToolCallEvent: ConversationEvent
{
    /// <summary>Initializes a tool-call event.</summary>
    /// <param name="toolName">The nonblank display name of the requested tool.</param>
    /// <param name="argumentsJson">The requested arguments, serialized as JSON text.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="argumentsJson"/> is <see langword="null"/>.</exception>
    public ConversationToolCallEvent(string toolName, string argumentsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(argumentsJson);
        ToolName = toolName;
        ArgumentsJson = argumentsJson;
    }

    /// <summary>Gets the display name of the requested tool.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets the requested arguments, serialized as JSON text.</summary>
    public string ArgumentsJson { get; init; }
}
