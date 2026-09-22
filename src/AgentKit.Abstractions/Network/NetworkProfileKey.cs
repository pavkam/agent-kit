// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A validated non-empty semantic key selecting one registered network profile
/// and its resolver/transport pair.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>.
/// </remarks>
public readonly record struct NetworkProfileKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkProfileKey"/> struct,
    /// validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty profile key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public NetworkProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the profile key text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
