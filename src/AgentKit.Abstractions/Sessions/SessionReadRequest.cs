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
    /// Read entries strictly after this sequence. Branch sequences are
    /// 1-indexed (the first entry ever appended to a branch has sequence 1),
    /// so <c>new SessionSequence(0)</c> reads from the very beginning
    /// without requiring a negative "before all" sentinel.
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

    /// <summary>Gets the operation context for this read.</summary>
    public SessionOperationContext Context { get; init; }

    /// <summary>Gets the branch to read from.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the sequence after which entries are read.</summary>
    public SessionSequence FromSequenceExclusive { get; init; }

    /// <summary>Gets the maximum number of entries to return.</summary>
    public int PageSize { get; init; }
}
