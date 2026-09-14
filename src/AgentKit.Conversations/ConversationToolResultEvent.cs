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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(summary);
        ToolName = toolName;
        Succeeded = succeeded;
        Summary = summary;
    }

    /// <summary>Gets the display name of the completed tool.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets whether the tool call completed successfully.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Gets the rendered result or safe failure reason.</summary>
    public string Summary { get; init; }
}
