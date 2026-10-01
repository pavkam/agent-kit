// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Is the immutable, validated copy of <see cref="AgentArtifactOptions"/> one keyed coordinator captured at registration.</summary>
/// <param name="MaximumArtifactBytes">The largest complete artifact accepted, in bytes.</param>
/// <param name="CopyBufferBytes">The stream copy buffer, in bytes.</param>
/// <param name="PreparationLifetime">The unpublished staging lifetime.</param>
/// <param name="OrphanRetention">The minimum retention of a reference-commit intent before collection.</param>
/// <param name="RequireDeclaredContentHash">Whether metadata must declare a hash.</param>
/// <param name="ProcessOutputDirectory">The explicit process-output directory, or <see langword="null"/> for the profile default.</param>
/// <param name="ProcessOutputRetentionPolicy">The retention policy recorded for process output.</param>
/// <param name="ProcessOutputClassification">The classification recorded for process output.</param>
internal sealed record AgentArtifactOptionsSnapshot(
    long MaximumArtifactBytes,
    int CopyBufferBytes,
    TimeSpan PreparationLifetime,
    TimeSpan OrphanRetention,
    bool RequireDeclaredContentHash,
    ArtifactDirectoryId? ProcessOutputDirectory,
    ArtifactRetentionPolicyKey ProcessOutputRetentionPolicy,
    DataClassification ProcessOutputClassification)
{
    /// <summary>Validates and copies mutable options.</summary>
    /// <param name="options">The configured options.</param>
    /// <returns>An immutable snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound or duration is not positive, the classification is undefined, or a key is blank.</exception>
    internal static AgentArtifactOptionsSnapshot Create(AgentArtifactOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumArtifactBytes, nameof(options.MaximumArtifactBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.CopyBufferBytes, nameof(options.CopyBufferBytes));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.PreparationLifetime, TimeSpan.Zero, nameof(options.PreparationLifetime));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.OrphanRetention, TimeSpan.Zero, nameof(options.OrphanRetention));
        ArgumentOutOfRangeException.ThrowIfUndefined(options.ProcessOutputClassification, nameof(options.ProcessOutputClassification));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProcessOutputRetentionPolicy.Value, nameof(options.ProcessOutputRetentionPolicy));
        if (options.ProcessOutputDirectory is { } directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory.Value, nameof(options.ProcessOutputDirectory));
        }

        return new(
            options.MaximumArtifactBytes,
            options.CopyBufferBytes,
            options.PreparationLifetime,
            options.OrphanRetention,
            options.RequireDeclaredContentHash,
            options.ProcessOutputDirectory,
            options.ProcessOutputRetentionPolicy,
            options.ProcessOutputClassification);
    }
}
