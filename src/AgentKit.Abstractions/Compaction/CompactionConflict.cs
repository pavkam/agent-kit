// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A compaction attempt that could not proceed or activate because the
/// branch is not at the version the attempt was computed against.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This is an
/// optimistic-concurrency signal, not a policy decision or a failure; a
/// caller may safely retry the attempt against the current branch version.
/// </para>
/// <para>
/// A conflict can be detected at two points. When the source read observes a
/// branch version other than <see cref="ExpectedVersion"/>, the attempt stops
/// before any strategy runs and <see cref="Manifest"/> is <see langword="null"/>
/// because no candidate was produced. When the branch advances between the
/// source read and activation, the version-checked append fails and
/// <see cref="Manifest"/> describes the validated candidate that could not be
/// activated.
/// </para>
/// </remarks>
public sealed record CompactionConflict: CompactionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompactionConflict"/> record for a stale source version
    /// detected before any candidate was produced.
    /// </summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="expectedVersion">The branch version the attempt was computed against.</param>
    /// <param name="actualVersion">The branch version actually observed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public CompactionConflict(
        CompactionOperationContext context,
        SessionVersion expectedVersion,
        SessionVersion actualVersion)
        : base(context)
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactionConflict"/> record for a validated candidate whose
    /// activation was rejected because the branch advanced.
    /// </summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="expectedVersion">The branch version the attempt was computed against.</param>
    /// <param name="actualVersion">The branch's current version.</param>
    /// <param name="manifest">The manifest for the candidate that could not be activated.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="manifest"/> is null.
    /// </exception>
    public CompactionConflict(
        CompactionOperationContext context,
        SessionVersion expectedVersion,
        SessionVersion actualVersion,
        CompactionManifest manifest)
        : this(context, expectedVersion, actualVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Manifest = manifest;
    }

    /// <summary>Gets the branch version the attempt was computed against.</summary>
    public SessionVersion ExpectedVersion { get; init; }

    /// <summary>Gets the branch version actually observed.</summary>
    public SessionVersion ActualVersion { get; init; }

    /// <summary>
    /// Gets the manifest for the candidate that could not be activated.
    /// </summary>
    /// <value>
    /// The validated candidate's manifest when the conflict surfaced at activation, or <see langword="null"/>
    /// when the stale version was observed during the source read before any candidate existed.
    /// </value>
    public CompactionManifest? Manifest { get; init; }
}
