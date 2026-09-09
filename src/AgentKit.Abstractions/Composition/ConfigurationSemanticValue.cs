// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one owned value from the initial closed effective-configuration semantic family.</summary>
/// <remarks>New selection families require an explicit derived case and compiler support; arbitrary strings and CLR activation are not escape hatches.</remarks>
public abstract record ConfigurationSemanticValue
{
    /// <summary>Restricts semantic variants to contracts defined by this assembly.</summary>
    /// <remarks>Concrete variants validate and own their specific document or typed publication evidence.</remarks>
    private protected ConfigurationSemanticValue()
    {
    }

    /// <summary>Allows generated record copies only when the source has the same concrete runtime type.</summary>
    /// <param name="original">The nonnull same-variant value being copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="original"/> has a different runtime type from the value under construction.</exception>
    /// <remarks>C# requires protected record copy construction; the type check prevents external records from bootstrapping a valid semantic variant from a built-in value.</remarks>
    protected ConfigurationSemanticValue(ConfigurationSemanticValue original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
