// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A request for one forward page of entries from a session branch, ordered
/// by sequence.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record SessionReadRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionReadRequest"/> record.</summary>
    /// <param name="context">The operation context for this read.</param>
    /// <param name="branchId">The branch to read from.</param>
    /// <param name="fromSequenceExclusive">
    /// Read entries strictly after this sequence within <paramref name="branchId"/>. Sequences are
    /// positive when allocated and are independent per branch, so <c>new SessionSequence(0)</c> reads from the
    /// very beginning without requiring a negative "before all" sentinel.
    /// </param>
    /// <param name="pageSize">The maximum number of entries to return.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageSize"/> is not positive.
    /// </exception>
    public SessionReadRequest(
        SessionOperationContext context,
        BranchId branchId,
        SessionSequence fromSequenceExclusive,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pageSize, 0);

        Context = context;
        BranchId = branchId;
        FromSequenceExclusive = fromSequenceExclusive;
        PageSize = pageSize;
    }

    /// <summary>Initializes a continuation read pinned to an exact previously captured branch prefix.</summary>
    /// <param name="context">The operation context for this read.</param>
    /// <param name="branchId">The branch to read from.</param>
    /// <param name="fromSequenceExclusive">Read entries strictly after this sequence.</param>
    /// <param name="pageSize">The positive maximum number of entries to return.</param>
    /// <param name="snapshot">The exact snapshot returned by the first page.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException">The snapshot address or branch differs from the request.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is not positive.</exception>
    public SessionReadRequest(
        SessionOperationContext context,
        BranchId branchId,
        SessionSequence fromSequenceExclusive,
        int pageSize,
        SessionReadSnapshot snapshot)
        : this(context, branchId, fromSequenceExclusive, pageSize)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNotEqual(snapshot.Address, context.ToAddress(), nameof(snapshot));
        ArgumentException.ThrowIfNotEqual(snapshot.BranchId, branchId, nameof(snapshot));
        Snapshot = snapshot;
    }

    /// <summary>Gets the operation context for this read.</summary>
    public SessionOperationContext Context { get; }

    /// <summary>Gets the branch to read from.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the sequence after which entries are read.</summary>
    public SessionSequence FromSequenceExclusive { get; }

    /// <summary>Gets the maximum number of entries to return.</summary>
    public int PageSize { get; }

    /// <summary>Gets the exact captured prefix for a continuation read.</summary>
    /// <value>The first page's snapshot, or null when requesting a new snapshot.</value>
    public SessionReadSnapshot? Snapshot { get; }
}
