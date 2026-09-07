// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies the semantic boundary a durable checkpoint was recorded at, so
/// recovery can reconstruct stable state without replaying fine-grained
/// progress.
/// </summary>
/// <remarks>
/// These are the boundaries at which committed state is meaningful. High
/// frequency stream deltas are deliberately absent: apparent complete text, a
/// successful-looking shell line, or a terminal-looking JSON fragment is not
/// evidence that anything settled externally.
/// </remarks>
public enum DurableCheckpointKind
{
    /// <summary>Input was admitted or promoted from a queue.</summary>
    InputAdmitted,

    /// <summary>
    /// A context and configuration manifest was created, fixing the versioned
    /// inputs for a model request.
    /// </summary>
    ContextManifestCreated,

    /// <summary>
    /// A provider response passed terminal validation and is safe to commit.
    /// </summary>
    ProviderResponseValidated,

    /// <summary>
    /// A tool call was recorded as accepted, immediately before its side
    /// effect could occur.
    /// </summary>
    ToolCallRecorded,

    /// <summary>
    /// One authoritative terminal tool outcome became available and is staged
    /// for source-ordered publication.
    /// </summary>
    ToolOutcomeReady,

    /// <summary>
    /// Assistant and tool messages were materialized into durable history in
    /// source order.
    /// </summary>
    MessagesCommitted,

    /// <summary>A compaction record was activated for the session.</summary>
    CompactionActivated,

    /// <summary>The run reached terminal settlement.</summary>
    RunSettled,
}
