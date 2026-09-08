// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that a durably activated compaction permits retrying an existing model request.</summary>
public sealed record CompactionRetryContinuationCause: RunContinuationCause
{
    /// <summary>Initializes activated compaction evidence.</summary>
    /// <param name="modelRequestId">The request made retryable by compaction.</param>
    /// <param name="compaction">The successfully activated compaction result.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modelRequestId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="compaction"/> is null.</exception>
    /// <exception cref="ArgumentException">The retained record does not remain active with a checkpoint.</exception>
    public CompactionRetryContinuationCause(ModelRequestId modelRequestId, CompactionSucceeded compaction)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        ArgumentNullException.ThrowIfNull(compaction);
        ArgumentException.ThrowIfCompactionNotActive(compaction);
        ModelRequestId = modelRequestId;
        Compaction = compaction;
    }

    /// <summary>Gets the retryable model request.</summary>
    public ModelRequestId ModelRequestId { get; }
    /// <summary>Gets the activated compaction evidence.</summary>
    public CompactionSucceeded Compaction { get; }
}
