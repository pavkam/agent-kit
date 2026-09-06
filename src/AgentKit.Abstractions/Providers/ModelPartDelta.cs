// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Carries one incremental fragment for the currently open part at
/// <see cref="PartIndex"/>.
/// </summary>
public sealed record ModelPartDelta: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelPartDelta"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="partIndex">The index of the open part this fragment belongs to.</param>
    /// <param name="delta">The incremental content fragment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="delta"/> is null.</exception>
    public ModelPartDelta(ModelRequestId requestId, long sequence, int partIndex, ContentDelta delta)
        : base(requestId, sequence)
    {
        ArgumentNullException.ThrowIfNull(delta);

        PartIndex = partIndex;
        Delta = delta;
    }

    /// <summary>Gets the index of the open part this fragment belongs to.</summary>
    public int PartIndex { get; init; }

    /// <summary>Gets the incremental content fragment.</summary>
    public ContentDelta Delta { get; init; }
}
