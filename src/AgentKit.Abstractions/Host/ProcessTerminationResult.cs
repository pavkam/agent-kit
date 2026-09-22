// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports whether termination completed and with what certainty.</summary>
public sealed record ProcessTerminationResult
{
    /// <summary>Initializes a termination result.</summary>
    /// <param name="completed">Whether termination completed according to policy.</param>
    /// <param name="sideEffectCertainty">What the host can prove about effects.</param>
    public ProcessTerminationResult(bool completed, SideEffectCertainty sideEffectCertainty)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        Completed = completed;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets whether termination completed according to policy.</summary>
    public bool Completed { get; init; }

    /// <summary>Gets what the host can prove about effects.</summary>
    public SideEffectCertainty SideEffectCertainty { get; init; }
}
