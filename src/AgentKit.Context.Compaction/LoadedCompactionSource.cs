// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// The outcome of one successful source load by <see cref="DefaultCompactor"/>:
/// the eligible snapshot handed to the cut selector, strategy, and validator,
/// plus the branch facts the compactor keeps for itself.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Snapshot"/> contains only entries whose sequence does not exceed
/// <see cref="CompactionRequest.SourceThrough"/>; downstream collaborators never
/// see the ineligible tail. <see cref="BranchTip"/> is the last sequence actually
/// committed to the branch at the time of the read, which is what a new entry's
/// sequence must follow. <see cref="IneligibleTailParents"/> is the set of
/// <see cref="SessionEntry.CausalParentId"/> values referenced by entries beyond
/// the eligible bound, so the compactor can refuse a cut that would separate an
/// ineligible retained entry from a covered causal parent the selector could not
/// see.
/// </para>
/// <para>
/// This type is an immutable value with structural equality and is safe to share
/// across threads.
/// </para>
/// </remarks>
internal sealed record LoadedCompactionSource
{
    /// <summary>Initializes a new instance of the <see cref="LoadedCompactionSource"/> record.</summary>
    /// <param name="snapshot">The bounded snapshot of eligible entries, in ascending sequence order.</param>
    /// <param name="branchTip">The last committed sequence on the branch at read time; zero for an empty branch.</param>
    /// <param name="ineligibleTailParents">
    /// The causal parent identities referenced by entries after <see cref="CompactionRequest.SourceThrough"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshot"/> or <paramref name="ineligibleTailParents"/> is null.
    /// </exception>
    public LoadedCompactionSource(
        CompactionSourceSnapshot snapshot,
        SessionSequence branchTip,
        ImmutableHashSet<SessionEntryId> ineligibleTailParents)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ineligibleTailParents);

        Snapshot = snapshot;
        BranchTip = branchTip;
        IneligibleTailParents = ineligibleTailParents;
    }

    /// <summary>Gets the bounded snapshot of eligible entries, in ascending sequence order.</summary>
    public CompactionSourceSnapshot Snapshot { get; }

    /// <summary>Gets the last committed sequence on the branch at read time; zero for an empty branch.</summary>
    public SessionSequence BranchTip { get; }

    /// <summary>Gets the causal parent identities referenced by entries after the eligible bound.</summary>
    public ImmutableHashSet<SessionEntryId> IneligibleTailParents { get; }
}
