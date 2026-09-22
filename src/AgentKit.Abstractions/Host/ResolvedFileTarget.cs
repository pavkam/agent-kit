// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A file target after root binding, host resolution, and link evidence capture.
/// </summary>
/// <remarks>
/// The security request binds <see cref="TargetFingerprint"/> and the effecting
/// implementation revalidates the same evidence immediately before acting.
/// </remarks>
public sealed record ResolvedFileTarget
{
    /// <summary>Initializes a new instance of the <see cref="ResolvedFileTarget"/> record.</summary>
    /// <param name="rootId">The configured root that owns the target.</param>
    /// <param name="relativePath">The normalized path relative to <paramref name="rootId"/>.</param>
    /// <param name="hostTargetPath">
    /// The normalized absolute host path the implementation will observe or mutate.
    /// </param>
    /// <param name="comparisonKind">The path comparison policy for this root.</param>
    /// <param name="linkResolutionEvidence">
    /// Fingerprinted evidence of symbolic-link resolution, or a sentinel when no
    /// link was traversed.
    /// </param>
    /// <param name="targetFingerprint">
    /// The fingerprint bound by the security request for this exact target.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="hostTargetPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public ResolvedFileTarget(
        FileRootId rootId,
        NormalizedRelativePath relativePath,
        string hostTargetPath,
        FilePathComparisonKind comparisonKind,
        ContentHash linkResolutionEvidence,
        ContentHash targetFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostTargetPath);
        RootId = rootId;
        RelativePath = relativePath;
        HostTargetPath = hostTargetPath;
        ComparisonKind = comparisonKind;
        LinkResolutionEvidence = linkResolutionEvidence;
        TargetFingerprint = targetFingerprint;
    }

    /// <summary>Gets the configured root that owns the target.</summary>
    public FileRootId RootId { get; init; }

    /// <summary>Gets the normalized path relative to <see cref="RootId"/>.</summary>
    public NormalizedRelativePath RelativePath { get; init; }

    /// <summary>Gets the normalized absolute host path the implementation will use.</summary>
    public string HostTargetPath { get; init; }

    /// <summary>Gets the path comparison policy for this root.</summary>
    public FilePathComparisonKind ComparisonKind { get; init; }

    /// <summary>Gets fingerprinted symbolic-link resolution evidence.</summary>
    public ContentHash LinkResolutionEvidence { get; init; }

    /// <summary>Gets the fingerprint bound by the security request.</summary>
    public ContentHash TargetFingerprint { get; init; }
}
