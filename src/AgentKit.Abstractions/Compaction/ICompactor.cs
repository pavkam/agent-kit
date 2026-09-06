// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Orchestrates one complete compaction attempt: loading the eligible
/// source, selecting a cut, producing a checkpoint, validating the
/// candidate, and durably activating it against the session.
/// </summary>
/// <remarks>
/// A compactor composes exactly one <see cref="ICompactionCutSelector"/>,
/// one <see cref="ICompactionStrategy"/>, and one
/// <see cref="ICompactionValidator"/>; it never re-implements their
/// responsibilities inline. Activation is expressed as an ordinary
/// version-checked append through <see cref="ISessionStore"/>, so a
/// concurrent append between source-load and activation surfaces as
/// <see cref="CompactionConflict"/> rather than silently overwriting
/// newer history. Implementations must be safe to call concurrently for
/// independent requests.
/// </remarks>
public interface ICompactor
{
    /// <summary>Runs one complete compaction attempt.</summary>
    /// <param name="request">The compaction request.</param>
    /// <param name="cancellationToken">A token used to cancel the attempt.</param>
    /// <returns>
    /// A task that resolves to the closed outcome of the attempt.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<CompactionResult> CompactAsync(CompactionRequest request, CancellationToken cancellationToken = default);
}
