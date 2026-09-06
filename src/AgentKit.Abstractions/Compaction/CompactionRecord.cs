// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The durable outcome of one compaction attempt, carried by a
/// <see cref="CompactionSessionEntry"/> in the session's append-only
/// record.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller record shape
/// described by the context-compaction architecture, which additionally
/// distinguishes a superseded historical candidate from a currently active
/// one and records commit-state detail on failure. This shape keeps only
/// the two states a first-party compactor without a background candidate
/// pipeline actually produces: <see cref="CompactionRecordStatus.Active"/>
/// for a successfully activated compaction, and
/// <see cref="CompactionRecordStatus.Rejected"/> for one that was decided
/// against and never activated.
/// </para>
/// </remarks>
public sealed record CompactionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CompactionRecord"/> record.</summary>
    /// <param name="context">The operation context this record was produced for.</param>
    /// <param name="sourceVersion">The branch version this attempt was computed against.</param>
    /// <param name="activatedSessionVersion">
    /// The branch version at which this record was appended, when
    /// <paramref name="status"/> is <see cref="CompactionRecordStatus.Active"/>.
    /// </param>
    /// <param name="status">The activation status of this record.</param>
    /// <param name="manifest">The structural manifest for the underlying candidate.</param>
    /// <param name="checkpoint">
    /// The checkpoint content, present when <paramref name="status"/> is
    /// <see cref="CompactionRecordStatus.Active"/>.
    /// </param>
    /// <param name="supersedes">
    /// The prior active compaction this one replaces, when applicable.
    /// </param>
    /// <param name="rejection">
    /// Why this attempt was rejected, present when <paramref name="status"/>
    /// is <see cref="CompactionRecordStatus.Rejected"/>.
    /// </param>
    /// <param name="recordedAt">The time this record was produced.</param>
    /// <param name="extensions">Caller-specific or forward-compatible record data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="manifest"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="status"/> is <see cref="CompactionRecordStatus.Active"/>
    /// and <paramref name="checkpoint"/> or <paramref name="activatedSessionVersion"/>
    /// is null, or <paramref name="status"/> is
    /// <see cref="CompactionRecordStatus.Rejected"/> and
    /// <paramref name="rejection"/> is null.
    /// </exception>
    public CompactionRecord(
        CompactionOperationContext context,
        SessionVersion sourceVersion,
        SessionVersion? activatedSessionVersion,
        CompactionRecordStatus status,
        CompactionManifest manifest,
        CompactionCheckpoint? checkpoint,
        CompactionId? supersedes,
        CompactionRejection? rejection,
        DateTimeOffset recordedAt,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(extensions);

        if (status == CompactionRecordStatus.Active && (checkpoint is null || activatedSessionVersion is null))
        {
            throw new ArgumentException(
                "An active compaction record must carry a checkpoint and an activated session version.",
                nameof(status));
        }

        if (status == CompactionRecordStatus.Rejected && rejection is null)
        {
            throw new ArgumentException("A rejected compaction record must carry a rejection.", nameof(status));
        }

        Context = context;
        SourceVersion = sourceVersion;
        ActivatedSessionVersion = activatedSessionVersion;
        Status = status;
        Manifest = manifest;
        Checkpoint = checkpoint;
        Supersedes = supersedes;
        Rejection = rejection;
        RecordedAt = recordedAt;
        Extensions = extensions;
    }

    /// <summary>Gets the operation context this record was produced for.</summary>
    public CompactionOperationContext Context { get; init; }

    /// <summary>Gets the branch version this attempt was computed against.</summary>
    public SessionVersion SourceVersion { get; init; }

    /// <summary>
    /// Gets the branch version at which this record was appended, when
    /// <see cref="Status"/> is <see cref="CompactionRecordStatus.Active"/>.
    /// </summary>
    public SessionVersion? ActivatedSessionVersion { get; init; }

    /// <summary>Gets the activation status of this record.</summary>
    public CompactionRecordStatus Status { get; init; }

    /// <summary>Gets the structural manifest for the underlying candidate.</summary>
    public CompactionManifest Manifest { get; init; }

    /// <summary>
    /// Gets the checkpoint content, present when <see cref="Status"/> is
    /// <see cref="CompactionRecordStatus.Active"/>.
    /// </summary>
    public CompactionCheckpoint? Checkpoint { get; init; }

    /// <summary>Gets the prior active compaction this one replaces, when applicable.</summary>
    public CompactionId? Supersedes { get; init; }

    /// <summary>
    /// Gets why this attempt was rejected, present when <see cref="Status"/>
    /// is <see cref="CompactionRecordStatus.Rejected"/>.
    /// </summary>
    public CompactionRejection? Rejection { get; init; }

    /// <summary>Gets the time this record was produced.</summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Gets caller-specific or forward-compatible record data.</summary>
    public ExtensionData Extensions { get; init; }
}
