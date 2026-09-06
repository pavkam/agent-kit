// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete, immutable input to one cut-selection attempt.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionCutSelectionRequest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCutSelectionRequest"/> record.</summary>
    /// <param name="request">The compaction request this selection serves.</param>
    /// <param name="source">The source snapshot to select a cut over.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> or <paramref name="source"/> is null.
    /// </exception>
    public CompactionCutSelectionRequest(CompactionRequest request, CompactionSourceSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        Request = request;
        Source = source;
    }

    /// <summary>Gets the compaction request this selection serves.</summary>
    public CompactionRequest Request { get; init; }

    /// <summary>Gets the source snapshot to select a cut over.</summary>
    public CompactionSourceSnapshot Source { get; init; }
}
