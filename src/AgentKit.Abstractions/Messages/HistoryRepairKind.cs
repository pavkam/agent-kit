// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the normative operation used to repair a history projection.</summary>
/// <remarks>Numeric order is not repair precedence or policy.</remarks>
public enum HistoryRepairKind
{
    /// <summary>Normalized portable message content.</summary>
    NormalizedContent,
    /// <summary>Settled an interrupted tool call with bounded terminal evidence.</summary>
    SettledInterruptedToolCall,
    /// <summary>Excluded incomplete assistant content.</summary>
    ExcludedIncompleteAssistantContent,
    /// <summary>Degraded reasoning evidence to a portable representation.</summary>
    DegradedReasoning,
    /// <summary>Omitted reasoning that could not be projected safely.</summary>
    OmittedReasoning,
    /// <summary>Relocated media to preserve valid provider-neutral ordering.</summary>
    RelocatedMedia,
    /// <summary>Removed provider metadata that was not eligible for projection.</summary>
    RemovedProviderMetadata,
    /// <summary>Merged adjacent user content without changing source order.</summary>
    MergedAdjacentUserContent,
    /// <summary>Normalized a tool-call identity while retaining correlation evidence.</summary>
    NormalizedToolCallIdentity,
    /// <summary>Projected an imported orphan tool call without fabricating an authoritative execution record.</summary>
    ProjectedImportedOrphanToolCall,
    /// <summary>Excluded a non-assistant message whose <see cref="MessageState"/> is not <see cref="MessageState.Complete"/>.</summary>
    ExcludedIncompleteMessage,
    /// <summary>Excluded a system or developer message found inside conversation history, because history never carries instruction authority.</summary>
    ExcludedInstructionMessage,
}
