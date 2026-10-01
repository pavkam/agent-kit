// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a tool-call record is durable.</summary>
public sealed record ToolCallRecorded: ToolCallRecordResult
{
    /// <summary>Initializes a successful recording outcome.</summary>
    /// <param name="replayed">Whether an identical earlier record already existed and was acknowledged instead of written again.</param>
    public ToolCallRecorded(bool replayed = false) => Replayed = replayed;

    /// <summary>Gets whether the write was an idempotent replay of an identical committed record.</summary>
    /// <value><see langword="true"/> when no new record was written; the durable fact is equally established either way.</value>
    public bool Replayed { get; }
}
