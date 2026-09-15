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
        : this(default, toolName, argumentsJson)
    {
    }

    /// <summary>Initializes a correlated tool-call event.</summary>
    /// <param name="callId">The typed call identity preserved through execution and result projection.</param>
    /// <param name="toolName">The nonblank display name of the requested tool.</param>
    /// <param name="argumentsJson">The requested arguments, serialized as JSON text.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="argumentsJson"/> is null.</exception>
    public ConversationToolCallEvent(ToolCallId callId, string toolName, string argumentsJson)
        : this(callId, toolName, argumentsJson, presentation: null)
    {
    }

    /// <summary>Initializes a correlated tool-call event with optional bounded presentation.</summary>
    /// <param name="callId">The typed call identity preserved through execution and result projection.</param>
    /// <param name="toolName">The nonblank alias advertised to the model.</param>
    /// <param name="argumentsJson">The requested arguments serialized as JSON text.</param>
    /// <param name="presentation">The optional bounded observational presentation.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="argumentsJson"/> is null.</exception>
    public ConversationToolCallEvent(
        ToolCallId callId,
        string toolName,
        string argumentsJson,
        ToolPresentation? presentation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(argumentsJson);
        CallId = callId;
        ToolName = toolName;
        ArgumentsJson = argumentsJson;
        Presentation = presentation;
    }

    /// <summary>Gets the typed call identity, or the default value for events created by the legacy constructor.</summary>
    public ToolCallId CallId { get; init; }

    /// <summary>Gets the display name of the requested tool.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets the requested arguments, serialized as JSON text.</summary>
    public string ArgumentsJson { get; init; }

    /// <summary>Gets the bounded observational presentation when a presenter was composed.</summary>
    /// <value>A presentation derived from the original call projection, or null when presentation was unavailable.</value>
    public ToolPresentation? Presentation { get; }
}
