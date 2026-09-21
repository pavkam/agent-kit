// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Structural and causality validation shared by history preparation and context assembly.</summary>
internal static class HistoryMessageValidation
{
    /// <summary>Validates role/part combinations over one message sequence.</summary>
    /// <param name="messages">The messages to validate in order.</param>
    /// <returns>A history failure when a violation is found; otherwise <see langword="null"/>.</returns>
    internal static HistoryFailure? ValidateRolePartCombinations(ImmutableArray<AgentMessage> messages)
    {
        Debug.Assert(!messages.IsDefault, "Callers must pass an initialized message array.");
        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                var violation = part switch
                {
                    ToolCallPart when message is not AssistantMessage =>
                        "History carries a tool call in a message whose role cannot request tools; only assistant messages may.",
                    ToolResultPart when message is not ToolMessage =>
                        "History carries a tool result in a message whose role cannot report results; only tool messages may.",
                    _ => null,
                };
                if (violation is not null)
                {
                    return new HistoryFailure(
                        HistoryFailureKind.InvalidRolePartCombination,
                        violation,
                        ExtensionData.Empty);
                }
            }
        }

        return null;
    }

    /// <summary>Validates tool-call causality over one message sequence.</summary>
    /// <param name="messages">The messages to validate in order.</param>
    /// <returns>A history failure when causality is broken; otherwise <see langword="null"/>.</returns>
    internal static HistoryFailure? ValidateToolCallCausality(ImmutableArray<AgentMessage> messages)
    {
        Debug.Assert(!messages.IsDefault, "Callers must pass an initialized message array.");
        var pendingCalls = new HashSet<ToolCallId>();
        var seenCalls = new HashSet<ToolCallId>();
        string? violation = null;

        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case ToolCallPart toolCall when !seenCalls.Add(toolCall.CallId):
                        violation = "History requested the same tool call identity more than once.";
                        break;
                    case ToolCallPart toolCall:
                        _ = pendingCalls.Add(toolCall.CallId);
                        break;
                    case ToolResultPart toolResult when !pendingCalls.Remove(toolResult.CallId):
                        violation = seenCalls.Contains(toolResult.CallId)
                            ? "History carries more than one terminal result for one tool call."
                            : "History carries a tool result that precedes, or has no, matching tool call.";
                        break;
                    default:
                        break;
                }

                if (violation is not null)
                {
                    return new HistoryFailure(
                        HistoryFailureKind.BrokenToolCallCausality,
                        violation,
                        ExtensionData.Empty);
                }
            }
        }

        return pendingCalls.Count == 0
            ? null
            : new HistoryFailure(
                HistoryFailureKind.BrokenToolCallCausality,
                "Every tool call in history must have exactly one matching terminal result.",
                ExtensionData.Empty);
    }
}
