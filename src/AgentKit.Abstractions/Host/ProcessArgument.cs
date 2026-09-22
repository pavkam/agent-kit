// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One structured process argument value.</summary>
public sealed record ProcessArgument
{
    /// <summary>Initializes one argument value.</summary>
    /// <param name="value">The exact argument text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null or contains NUL.</exception>
    public ProcessArgument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfContainsNul(value);
        Value = value;
    }

    /// <summary>Gets the exact argument text.</summary>
    public string Value { get; }
}
