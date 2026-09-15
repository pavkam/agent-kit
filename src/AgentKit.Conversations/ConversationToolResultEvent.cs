// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports the terminal outcome of one tool call committed during a conversational turn.</summary>
/// <remarks>
/// This is a display-oriented projection, not the authoritative execution record: <see cref="Summary"/> is
/// rendered text (the tool's textual result content on success, or its safe failure reason on rejection or
/// failure), not the tool's full structured result.
/// </remarks>
public sealed record ConversationToolResultEvent: ConversationEvent
{
    /// <summary>Initializes a tool-result event.</summary>
    /// <param name="toolName">The nonblank display name of the completed tool.</param>
    /// <param name="succeeded">Whether the tool call completed successfully.</param>
    /// <param name="summary">The nonnull rendered result or safe failure reason.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is <see langword="null"/>.</exception>
    public ConversationToolResultEvent(string toolName, bool succeeded, string summary)
        : this(default, toolName, succeeded, summary)
    {
    }

    /// <summary>Initializes a correlated tool-result event.</summary>
    /// <param name="callId">The typed call identity preserved from the model request.</param>
    /// <param name="toolName">The nonblank display name of the completed tool.</param>
    /// <param name="succeeded">Whether the tool call completed successfully.</param>
    /// <param name="summary">The nonnull bounded result content and safe failure detail.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is null.</exception>
    public ConversationToolResultEvent(ToolCallId callId, string toolName, bool succeeded, string summary)
        : this(callId, toolName, succeeded, summary, presentation: null)
    {
    }

    /// <summary>Initializes a correlated tool-result event with optional bounded presentation.</summary>
    /// <param name="callId">The typed call identity preserved from the model request.</param>
    /// <param name="toolName">The nonblank alias advertised to the model.</param>
    /// <param name="succeeded">Whether the projected terminal outcome is successful.</param>
    /// <param name="summary">The nonnull bounded generic summary.</param>
    /// <param name="presentation">The optional bounded observational presentation.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is null.</exception>
    public ConversationToolResultEvent(
        ToolCallId callId,
        string toolName,
        bool succeeded,
        string summary,
        ToolPresentation? presentation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(summary);
        CallId = callId;
        ToolName = toolName;
        Succeeded = succeeded;
        Summary = summary;
        Presentation = presentation;
    }

    /// <summary>Gets the typed call identity, or the default value for events created by the legacy constructor.</summary>
    public ToolCallId CallId { get; init; }

    /// <summary>Gets the display name of the completed tool.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets whether the tool call completed successfully.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Gets the rendered result or safe failure reason.</summary>
    public string Summary { get; init; }

    /// <summary>Gets the bounded observational presentation of the loss-aware result projection.</summary>
    /// <value>A presentation derived from the original <see cref="ToolResultPart"/>, or null when unavailable.</value>
    public ToolPresentation? Presentation { get; }
}
