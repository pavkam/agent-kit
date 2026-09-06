// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A compaction attempt whose validated candidate could not be activated
/// because the branch advanced past the version the attempt was computed
/// against.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This is an
/// optimistic-concurrency signal, not a policy decision or a failure; a
/// caller may safely retry the attempt against the current branch version.
/// </remarks>
public sealed record CompactionConflict: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionConflict"/> record.</summary>
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
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
        Manifest = manifest;
    }

    /// <summary>Gets the branch version the attempt was computed against.</summary>
    public SessionVersion ExpectedVersion { get; init; }

    /// <summary>Gets the branch's current version.</summary>
    public SessionVersion ActualVersion { get; init; }

    /// <summary>Gets the manifest for the candidate that could not be activated.</summary>
    public CompactionManifest Manifest { get; init; }
}
