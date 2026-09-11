// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines pre-admission rejection and an accepted run's final envelope as distinct result cases.</summary>
/// <typeparam name="TOutput">The validated output type whose ownership is defined by the finished result.</typeparam>
public abstract record AgentRunResult<TOutput>
{
    /// <summary>Closes the result family to the canonical cases in this assembly.</summary>
    /// <remarks>The base introduces no fabricated run identity, mutable state, or settlement effects.</remarks>
    private protected AgentRunResult() { }

    /// <summary>Copies only the same canonical variant, preventing external variants from copying a built-in value.</summary>
    /// <param name="original">The nonnull value with the same concrete runtime type.</param>
    /// <exception cref="ArgumentNullException">The original is null.</exception>
    /// <exception cref="ArgumentException">The original has a different concrete runtime type.</exception>
    /// <remarks>Record inheritance requires a protected copy constructor. Same-variant record copies remain valid without reopening this closed family.</remarks>
    protected AgentRunResult(AgentRunResult<TOutput> original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
