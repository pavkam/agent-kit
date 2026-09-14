// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports one model response's usage evidence committed during a conversational turn.</summary>
/// <remarks>
/// Raised immediately after the <see cref="ConversationAssistantTextEvent"/> and/or
/// <see cref="ConversationToolCallEvent"/> instances projected from the same assistant response, and only when
/// that response actually carried usage evidence (<see cref="ModelUsage.ReportState"/> is not
/// <see cref="ModelUsageReportState.NotReported"/>) — a provider or model that never reports usage never raises
/// this event.
/// </remarks>
public sealed record ConversationUsageEvent: ConversationEvent
{
    /// <summary>Initializes a usage event.</summary>
    /// <param name="usage">The reported usage evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="usage"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="usage"/> reports <see cref="ModelUsageReportState.NotReported"/>.</exception>
    public ConversationUsageEvent(ModelUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        if (usage.ReportState == ModelUsageReportState.NotReported)
        {
            throw new ArgumentException("Usage evidence must actually be reported.", nameof(usage));
        }

        Usage = usage;
    }

    /// <summary>Gets the reported usage evidence.</summary>
    public ModelUsage Usage { get; init; }
}
