// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that reconciliation produced updated side-effect certainty.</summary>
public sealed record DurableReconciled: DurableReconciliationResult
{
    /// <summary>Initializes a successful reconciliation.</summary>
    /// <param name="sideEffectCertainty">The defined certainty after reconciliation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sideEffectCertainty"/> is undefined.</exception>
    public DurableReconciled(SideEffectCertainty sideEffectCertainty)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the reconciled side-effect certainty.</summary>
    /// <value>
    /// A defined <see cref="SideEffectCertainty"/> member describing the relevant external effect as the external
    /// owner now reports it. It never describes whether a result record was written.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set an undefined member.</exception>
    public SideEffectCertainty SideEffectCertainty
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(SideEffectCertainty));
            field = value;
        }
    }
}
