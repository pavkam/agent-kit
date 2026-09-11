// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Separates settlement completion or recovery needs from the immutable semantic run outcome.</summary>
public abstract record RunSettlementOutcome
{
    /// <summary>Closes the result family to the canonical cases in this assembly.</summary>
    /// <remarks>The base introduces no fabricated run identity, mutable state, or settlement effects.</remarks>
    private protected RunSettlementOutcome() { }

    /// <summary>Copies only the same canonical variant, preventing external variants from copying a built-in value.</summary>
    /// <param name="original">The nonnull value with the same concrete runtime type.</param>
    /// <exception cref="ArgumentNullException">The original is null.</exception>
    /// <exception cref="ArgumentException">The original has a different concrete runtime type.</exception>
    /// <remarks>Record inheritance requires a protected copy constructor. Same-variant record copies remain valid without reopening this closed family.</remarks>
    protected RunSettlementOutcome(RunSettlementOutcome original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
