// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounded compaction outcome evidence for event publication.</summary>
public sealed record CompactionOutcomeSummary
{
    /// <summary>Initializes a new instance of the <see cref="CompactionOutcomeSummary"/> record.</summary>
    /// <param name="kind">The normalized outcome kind.</param>
    /// <param name="manifestId">The manifest identity when produced.</param>
    /// <param name="recordVersion">The record version when activated.</param>
    /// <param name="activatedSessionVersion">The activated session version when known.</param>
    /// <param name="safeMessage">An optional non-sensitive explanation.</param>
    public CompactionOutcomeSummary(
        CompactionOutcomeKind kind,
        CompactionManifestId? manifestId,
        CompactionRecordVersion? recordVersion,
        SessionVersion? activatedSessionVersion,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        if (safeMessage is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        }

        Kind = kind;
        ManifestId = manifestId;
        RecordVersion = recordVersion;
        ActivatedSessionVersion = activatedSessionVersion;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the normalized outcome kind.</summary>
    public CompactionOutcomeKind Kind { get; }

    /// <summary>Gets the manifest identity when produced.</summary>
    public CompactionManifestId? ManifestId { get; }

    /// <summary>Gets the record version when activated.</summary>
    public CompactionRecordVersion? RecordVersion { get; }

    /// <summary>Gets the activated session version when known.</summary>
    public SessionVersion? ActivatedSessionVersion { get; }

    /// <summary>Gets an optional non-sensitive explanation.</summary>
    public string? SafeMessage { get; }
}
