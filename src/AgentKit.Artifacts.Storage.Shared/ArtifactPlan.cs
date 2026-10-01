// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the complete, ordered effect of one planned lifecycle operation over a consistent entry view.</summary>
/// <remarks>
/// An adapter executes a plan in one order: stage the payload, persist the upserts, then release payloads. Because a tombstone or
/// aborted entry is persisted before its payload is released, a crash can leave unreferenced bytes but never a committed entry
/// without bytes or readable bytes without an entry.
/// </remarks>
/// <typeparam name="TResult">The store result returned once the plan is durably applied.</typeparam>
/// <param name="Result">The result returned after a successful commit of the plan.</param>
internal sealed record ArtifactPlan<TResult>(TResult Result)
{
    /// <summary>Gets the entries to insert or replace, in order.</summary>
    internal ImmutableArray<ArtifactEntry> Upserts { get; init; } = [];

    /// <summary>Gets the payload to stage before the entries are persisted, or <see langword="null"/> when none.</summary>
    internal ArtifactPayloadStage? Stage { get; init; }

    /// <summary>Gets the entries whose payloads are released after the entries are persisted.</summary>
    internal ImmutableArray<ArtifactEntry> Releases { get; init; } = [];
}
