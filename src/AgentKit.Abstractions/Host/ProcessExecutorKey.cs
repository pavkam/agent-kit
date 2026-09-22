// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A validated non-empty semantic key selecting one registered process executor profile.</summary>
public readonly record struct ProcessExecutorKey
{
    /// <summary>Initializes a new instance of the <see cref="ProcessExecutorKey"/> struct.</summary>
    /// <param name="value">The non-empty profile key text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ProcessExecutorKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the profile key text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
