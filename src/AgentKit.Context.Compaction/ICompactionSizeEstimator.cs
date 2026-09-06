// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// Produces an approximate <see cref="CompactionSizeEstimate"/> for a set of
/// session entries or a checkpoint, without requiring a real provider
/// tokenizer.
/// </summary>
/// <remarks>
/// This is a replaceable, additive-free singleton service: an application
/// with access to a real tokenizer for its target model registers its own
/// implementation in place of <see cref="CharacterCompactionSizeEstimator"/>
/// through <c>services.AddSingleton&lt;ICompactionSizeEstimator, T&gt;()</c>.
/// Implementations must be safe to call concurrently and must not mutate
/// any input they receive.
/// </remarks>
public interface ICompactionSizeEstimator
{
    /// <summary>Estimates the size of a set of session entries.</summary>
    /// <param name="entries">The entries to estimate.</param>
    /// <returns>An approximate size estimate covering every entry.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> is a default, uninitialized array.
    /// </exception>
    public CompactionSizeEstimate EstimateEntries(ImmutableArray<SessionEntry> entries);

    /// <summary>Estimates the size of a produced checkpoint's summary content.</summary>
    /// <param name="checkpoint">The checkpoint to estimate.</param>
    /// <returns>An approximate size estimate for the checkpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> is null.</exception>
    public CompactionSizeEstimate EstimateCheckpoint(CompactionCheckpoint checkpoint);
}
