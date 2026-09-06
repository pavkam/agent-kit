// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Signals that a new response part has opened at <see cref="PartIndex"/>.
/// Deltas and completion for that index are valid only after this event and
/// before the part is closed by a matching <see cref="ModelPartCompleted"/>.
/// </summary>
/// <remarks>
/// Different open parts may interleave, which preserves providers that
/// stream parallel tool-call arguments: more than one
/// <see cref="ModelPartStarted"/> may be outstanding at the same time for
/// distinct <see cref="PartIndex"/> values.
/// </remarks>
public sealed record ModelPartStarted: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelPartStarted"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="partIndex">The index of the part that has opened.</param>
    public ModelPartStarted(ModelRequestId requestId, long sequence, int partIndex)
        : base(requestId, sequence) => PartIndex = partIndex;

    /// <summary>Gets the index of the part that has opened.</summary>
    public int PartIndex { get; init; }
}
