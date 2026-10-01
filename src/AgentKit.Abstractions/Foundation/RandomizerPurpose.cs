// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The bounded, nonsecret purpose an operation-owned randomizer is created for, such as retry jitter.</summary>
/// <remarks>
/// This type is an immutable value object with ordinal textual equality. A default instance can exist in memory but
/// is never a valid purpose; every public boundary rejects it.
/// </remarks>
public readonly record struct RandomizerPurpose
{
    /// <summary>Initializes a validated <see cref="RandomizerPurpose"/> value.</summary>
    /// <param name="value">The nonblank identity text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace-only.</exception>
    public RandomizerPurpose(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the nonblank identity text.</summary>
    public string Value { get; }

    /// <summary>Returns the identity text.</summary>
    /// <returns>The identity text, or an empty string for a default instance.</returns>
    public override string ToString() => Value ?? string.Empty;
}
