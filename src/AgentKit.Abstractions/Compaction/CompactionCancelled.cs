// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compaction attempt that was cancelled before it could complete.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </para>
/// <para>
/// Cancellation alone does not say whether the checkpoint became durable.
/// <see cref="CommitState"/> reports what the compactor established after
/// observing cancellation: <see cref="CompactionCommitState.NotAttempted"/>
/// when no activation append was issued, <see cref="CompactionCommitState.NotCommitted"/>
/// or <see cref="CompactionCommitState.Committed"/> when reconciliation
/// settled the question, and <see cref="CompactionCommitState.Unknown"/> when
/// it could not. <see cref="CommittedRecord"/> is present exactly when the
/// state is <see cref="CompactionCommitState.Committed"/>.
/// </para>
/// </remarks>
public sealed record CompactionCancelled: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCancelled"/> record.</summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="commitState">What is known about the activation append when cancellation was observed.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="committedRecord">
    /// The record found durable on the branch; required when <paramref name="commitState"/> is
    /// <see cref="CompactionCommitState.Committed"/> and forbidden otherwise.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="commitState"/> is not a defined <see cref="CompactionCommitState"/> value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace; or
    /// <paramref name="committedRecord"/> is null while <paramref name="commitState"/> is
    /// <see cref="CompactionCommitState.Committed"/>; or <paramref name="committedRecord"/> is non-null while
    /// <paramref name="commitState"/> is any other value.
    /// </exception>
    public CompactionCancelled(
        CompactionOperationContext context,
        CompactionCommitState commitState,
        string safeMessage,
        CompactionRecord? committedRecord = null)
        : base(context)
    {
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

        CommitState = commitState;
        SafeMessage = safeMessage;
        CommittedRecord = committedRecord;
    }

    /// <summary>Gets what is known about the activation append when cancellation was observed.</summary>
    public CompactionCommitState CommitState { get; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>Gets the record found durable on the branch.</summary>
    /// <value>
    /// The committed record when <see cref="CommitState"/> is <see cref="CompactionCommitState.Committed"/>;
    /// otherwise <see langword="null"/>.
    /// </value>
    public CompactionRecord? CommittedRecord { get; }
}
