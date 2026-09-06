// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The structural description of one produced compaction candidate: what it
/// covers, who produced it, and how large it is, kept separate from the
/// checkpoint content itself.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Separating the manifest from
/// <see cref="CompactionCheckpoint"/> lets validators, audit sinks, and
/// retention policy reason about a candidate's shape without necessarily
/// loading its full summary content.
/// </remarks>
public sealed record CompactionManifest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionManifest"/> record.</summary>
    /// <param name="id">The identity of this specific manifest.</param>
    /// <param name="context">The operation context this manifest was produced for.</param>
    /// <param name="branchId">The branch this manifest covers.</param>
    /// <param name="sourceVersion">The branch version this manifest was computed against.</param>
    /// <param name="coveredRange">The inclusive range of sequences covered.</param>
    /// <param name="retainedSuffixStart">The first sequence of the untouched retained suffix.</param>
    /// <param name="producer">Provenance for how the checkpoint was produced.</param>
    /// <param name="contextEpoch">The context epoch this manifest was produced under.</param>
    /// <param name="before">The estimated size of the covered range before compaction.</param>
    /// <param name="after">The estimated size of the resulting checkpoint.</param>
    /// <param name="producedAt">The time this manifest was produced.</param>
    /// <param name="extensions">Caller-specific or forward-compatible manifest data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="coveredRange"/>,
    /// <paramref name="producer"/>, <paramref name="before"/>,
    /// <paramref name="after"/>, or <paramref name="extensions"/> is null.
    /// </exception>
    public CompactionManifest(
        CompactionManifestId id,
        CompactionOperationContext context,
        BranchId branchId,
        SessionVersion sourceVersion,
        CompactionSourceRange coveredRange,
        SessionSequence retainedSuffixStart,
        CompactionProducer producer,
        ContextEpoch contextEpoch,
        CompactionSizeEstimate before,
        CompactionSizeEstimate after,
        DateTimeOffset producedAt,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(coveredRange);
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(extensions);

        Id = id;
        Context = context;
        BranchId = branchId;
        SourceVersion = sourceVersion;
        CoveredRange = coveredRange;
        RetainedSuffixStart = retainedSuffixStart;
        Producer = producer;
        ContextEpoch = contextEpoch;
        Before = before;
        After = after;
        ProducedAt = producedAt;
        Extensions = extensions;
    }

    /// <summary>Gets the identity of this specific manifest.</summary>
    public CompactionManifestId Id { get; init; }

    /// <summary>Gets the operation context this manifest was produced for.</summary>
    public CompactionOperationContext Context { get; init; }

    /// <summary>Gets the branch this manifest covers.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the branch version this manifest was computed against.</summary>
    public SessionVersion SourceVersion { get; init; }

    /// <summary>Gets the inclusive range of sequences covered.</summary>
    public CompactionSourceRange CoveredRange { get; init; }

    /// <summary>Gets the first sequence of the untouched retained suffix.</summary>
    public SessionSequence RetainedSuffixStart { get; init; }

    /// <summary>Gets provenance for how the checkpoint was produced.</summary>
    public CompactionProducer Producer { get; init; }

    /// <summary>Gets the context epoch this manifest was produced under.</summary>
    public ContextEpoch ContextEpoch { get; init; }

    /// <summary>Gets the estimated size of the covered range before compaction.</summary>
    public CompactionSizeEstimate Before { get; init; }

    /// <summary>Gets the estimated size of the resulting checkpoint.</summary>
    public CompactionSizeEstimate After { get; init; }

    /// <summary>Gets the time this manifest was produced.</summary>
    public DateTimeOffset ProducedAt { get; init; }

    /// <summary>Gets caller-specific or forward-compatible manifest data.</summary>
    public ExtensionData Extensions { get; init; }
}
