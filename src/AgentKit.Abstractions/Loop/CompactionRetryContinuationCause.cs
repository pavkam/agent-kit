// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies evidence that a durably activated compaction permits retrying an existing model request.</summary>
/// <remarks>The cause retains the original request identity and does not invent a completed assistant response or authorize the retry.</remarks>
public sealed record CompactionRetryContinuationCause: RunContinuationCause
{
    /// <summary>Initializes evidence for a retry made eligible by activated compaction.</summary>
    /// <param name="modelRequestId">The non-default identity of the existing model request made retryable by compaction.</param>
    /// <param name="compaction">The non-null successfully activated compaction result that supplies retained-context evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modelRequestId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="compaction"/> is null.</exception>
    /// <exception cref="ArgumentException">The retained evidence has default identities, inconsistent contexts, or no active checkpoint.</exception>
    public CompactionRetryContinuationCause(ModelRequestId modelRequestId, CompactionSucceeded compaction)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        ArgumentNullException.ThrowIfNull(compaction);
        ArgumentException.ThrowIfCompactionNotActive(compaction);
        ModelRequestId = modelRequestId;
        Compaction = compaction;
    }

    /// <summary>Gets the identity of the existing model request eligible for retry.</summary>
    /// <value>A non-default request identity retained from the interrupted attempt.</value>
    public ModelRequestId ModelRequestId { get; }
    /// <summary>Gets the successfully activated compaction evidence.</summary>
    /// <value>A non-null result with an active checkpoint, retained for session-owner revalidation.</value>
    public CompactionSucceeded Compaction { get; }
}
