// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete, immutable input to one checkpoint-production attempt.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionStrategyRequest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionStrategyRequest"/> record.</summary>
    /// <param name="request">The compaction request this production serves.</param>
    /// <param name="source">The source snapshot the cut was selected over.</param>
    /// <param name="cut">The structurally safe cut to produce a checkpoint for.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/>, <paramref name="source"/>, or
    /// <paramref name="cut"/> is null.
    /// </exception>
    public CompactionStrategyRequest(CompactionRequest request, CompactionSourceSnapshot source, CompactionCut cut)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cut);

        Request = request;
        Source = source;
        Cut = cut;
    }

    /// <summary>Gets the compaction request this production serves.</summary>
    public CompactionRequest Request { get; init; }

    /// <summary>Gets the source snapshot the cut was selected over.</summary>
    public CompactionSourceSnapshot Source { get; init; }

    /// <summary>Gets the structurally safe cut to produce a checkpoint for.</summary>
    public CompactionCut Cut { get; init; }
}
