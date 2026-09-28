// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The journaled manifest of one compaction record's activation attempt.</summary>
/// <remarks>
/// <para>
/// The manifest identifies the logical checkpoint and the exact branch version the append expects, which is what a
/// recovering worker needs to decide whether the record it finds in the session is this attempt's or another's.
/// The summary text, the covered entries, and the retained suffix are content and never enter this record.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its fields. It carries no mutable state and
/// is safe to share across threads without synchronization.
/// </para>
/// </remarks>
public sealed record DurableCompactionActivationManifest
{
    /// <summary>Initializes one complete compaction-activation manifest.</summary>
    /// <param name="compactionId">The nonempty identity of the logical checkpoint being activated.</param>
    /// <param name="sourceVersion">The positive branch version the idempotent append must match.</param>
    /// <param name="activatedVersion">The branch version the record claims once committed; it must follow <paramref name="sourceVersion"/>.</param>
    /// <param name="coveredEntryCount">The nonnegative number of source entries the candidate covers.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="compactionId"/> is the empty identity, <paramref name="sourceVersion"/> is not positive,
    /// <paramref name="activatedVersion"/> does not follow <paramref name="sourceVersion"/>, or
    /// <paramref name="coveredEntryCount"/> is negative.
    /// </exception>
    public DurableCompactionActivationManifest(
        Guid compactionId,
        long sourceVersion,
        long activatedVersion,
        int coveredEntryCount)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(compactionId, Guid.Empty, nameof(compactionId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(activatedVersion, sourceVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(coveredEntryCount);
        CompactionId = compactionId;
        SourceVersion = sourceVersion;
        ActivatedVersion = activatedVersion;
        CoveredEntryCount = coveredEntryCount;
    }

    /// <summary>Gets the logical checkpoint being activated.</summary>
    /// <value>The nonempty compaction identity, which stays stable across a retried attempt.</value>
    public Guid CompactionId { get; }

    /// <summary>Gets the branch version the idempotent append must match.</summary>
    /// <value>A positive session version observed before the append.</value>
    public long SourceVersion { get; }

    /// <summary>Gets the branch version the activated record claims.</summary>
    /// <value>A session version strictly after <see cref="SourceVersion"/>.</value>
    public long ActivatedVersion { get; }

    /// <summary>Gets how many source entries the candidate covers.</summary>
    /// <value>A nonnegative count of covered entries; the entries themselves are not recorded here.</value>
    public int CoveredEntryCount { get; }
}
