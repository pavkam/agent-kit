// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A cut-selection attempt that found a usable cut.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionCutSelected: CompactionCutSelectionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCutSelected"/> record.</summary>
    /// <param name="cut">The selected cut.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cut"/> is null.</exception>
    public CompactionCutSelected(CompactionCut cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        Cut = cut;
    }

    /// <summary>Gets the selected cut.</summary>
    public CompactionCut Cut { get; init; }
}
