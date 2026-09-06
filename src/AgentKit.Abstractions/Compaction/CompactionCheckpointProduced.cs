// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A checkpoint-production attempt that produced a candidate.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionCheckpointProduced: CompactionStrategyResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCheckpointProduced"/> record.</summary>
    /// <param name="checkpoint">The produced checkpoint content.</param>
    /// <param name="producer">Provenance for how the checkpoint was produced.</param>
    /// <param name="after">The estimated size of the produced checkpoint.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="checkpoint"/>, <paramref name="producer"/>, or
    /// <paramref name="after"/> is null.
    /// </exception>
    public CompactionCheckpointProduced(
        CompactionCheckpoint checkpoint, CompactionProducer producer, CompactionSizeEstimate after)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(after);

        Checkpoint = checkpoint;
        Producer = producer;
        After = after;
    }

    /// <summary>Gets the produced checkpoint content.</summary>
    public CompactionCheckpoint Checkpoint { get; init; }

    /// <summary>Gets provenance for how the checkpoint was produced.</summary>
    public CompactionProducer Producer { get; init; }

    /// <summary>Gets the estimated size of the produced checkpoint.</summary>
    public CompactionSizeEstimate After { get; init; }
}
