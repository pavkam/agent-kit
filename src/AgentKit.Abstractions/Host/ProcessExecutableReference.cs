// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A configured executable path or resolver-owned alias.</summary>
public sealed record ProcessExecutableReference
{
    /// <summary>Initializes an executable reference.</summary>
    /// <param name="value">The non-empty configured executable text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, whitespace, or contains NUL.</exception>
    public ProcessExecutableReference(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfContainsNul(value);
        Value = value;
    }

    /// <summary>Gets the configured executable text.</summary>
    public string Value { get; }
}
