// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Closes the part at <see cref="PartIndex"/>, carrying the fully assembled
/// <see cref="Part"/> once its stream of deltas is complete.
/// </summary>
public sealed record ModelPartCompleted: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelPartCompleted"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="partIndex">The index of the part that has closed.</param>
    /// <param name="part">The fully assembled content part.</param>
    /// <exception cref="ArgumentNullException"><paramref name="part"/> is null.</exception>
    public ModelPartCompleted(ModelRequestId requestId, long sequence, int partIndex, ContentPart part)
        : base(requestId, sequence)
    {
        ArgumentNullException.ThrowIfNull(part);

        PartIndex = partIndex;
        Part = part;
    }

    /// <summary>Gets the index of the part that has closed.</summary>
    public int PartIndex { get; init; }

    /// <summary>Gets the fully assembled content part.</summary>
    public ContentPart Part { get; init; }
}
