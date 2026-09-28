// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the outcome of redacting one observation payload before export.</summary>
public abstract record RedactionResult
{
    /// <summary>Closes the result family to the canonical cases in this assembly.</summary>
    private protected RedactionResult() { }

    /// <summary>Copies only the same canonical variant.</summary>
    /// <param name="original">The nonnull value with the same concrete runtime type.</param>
    /// <exception cref="ArgumentNullException">The original is null.</exception>
    /// <exception cref="ArgumentException">The original has a different concrete runtime type.</exception>
    protected RedactionResult(RedactionResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
