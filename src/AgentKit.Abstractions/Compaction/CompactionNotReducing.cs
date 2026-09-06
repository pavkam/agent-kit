// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A compaction attempt whose candidate passed structural validation but
/// did not achieve the required measurable size reduction, and so was
/// rejected rather than activated.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A
/// compaction attempt that shrinks nothing, or shrinks by less than
/// <see cref="RequiredReductionRatio"/>, provides no benefit and risks
/// discarding source detail for no gain; the compactor rejects it instead
/// of activating a checkpoint that does not pay for itself.
/// </remarks>
public sealed record CompactionNotReducing: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionNotReducing"/> record.</summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="before">The estimated size of the covered range before compaction.</param>
    /// <param name="after">The estimated size of the produced checkpoint.</param>
    /// <param name="requiredReductionRatio">The minimum reduction ratio that was required.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="before"/>, or
    /// <paramref name="after"/> is null.
    /// </exception>
    public CompactionNotReducing(
        CompactionOperationContext context,
        CompactionSizeEstimate before,
        CompactionSizeEstimate after,
        double requiredReductionRatio)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        Before = before;
        After = after;
        RequiredReductionRatio = requiredReductionRatio;
    }

    /// <summary>Gets the estimated size of the covered range before compaction.</summary>
    public CompactionSizeEstimate Before { get; init; }

    /// <summary>Gets the estimated size of the produced checkpoint.</summary>
    public CompactionSizeEstimate After { get; init; }

    /// <summary>Gets the minimum reduction ratio that was required.</summary>
    public double RequiredReductionRatio { get; init; }
}
