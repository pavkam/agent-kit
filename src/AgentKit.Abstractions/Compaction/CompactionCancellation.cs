// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Cancellation evidence for one compaction stage.</summary>
public sealed record CompactionCancellation
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCancellation"/> record.</summary>
    /// <param name="reason">Why cancellation was observed.</param>
    /// <param name="commitState">What is known about activation when cancellation was observed.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="committedRecord">
    /// The record found durable on the branch; required when <paramref name="commitState"/> is
    /// <see cref="CompactionCommitState.Committed"/> and forbidden otherwise.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> or <paramref name="commitState"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or whitespace; or <paramref name="committedRecord"/> is null while
    /// <paramref name="commitState"/> is <see cref="CompactionCommitState.Committed"/>; or
    /// <paramref name="committedRecord"/> is non-null while <paramref name="commitState"/> is any other value.
    /// </exception>
    public CompactionCancellation(
        CompactionCancellationReason reason,
        CompactionCommitState commitState,
        string safeMessage,
        CompactionRecord? committedRecord = null)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        ArgumentOutOfRangeException.ThrowIfUndefined(commitState);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        if (commitState == CompactionCommitState.Committed && committedRecord is null)
        {
            throw new ArgumentException(
                "A committed cancellation outcome must carry the committed record.", nameof(committedRecord));
        }

        if (commitState != CompactionCommitState.Committed && committedRecord is not null)
        {
            throw new ArgumentException(
                "Only a committed cancellation outcome may carry a record.", nameof(committedRecord));
        }

        Reason = reason;
        CommitState = commitState;
        SafeMessage = safeMessage;
        CommittedRecord = committedRecord;
    }

    /// <summary>Gets why cancellation was observed.</summary>
    public CompactionCancellationReason Reason { get; }

    /// <summary>Gets what is known about activation when cancellation was observed.</summary>
    public CompactionCommitState CommitState { get; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; }

    /// <summary>Gets the record found durable on the branch when cancellation followed a committed append.</summary>
    public CompactionRecord? CommittedRecord { get; }
}
