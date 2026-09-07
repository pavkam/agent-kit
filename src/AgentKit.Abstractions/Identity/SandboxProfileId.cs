// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one versioned host-enforced process sandbox profile.</summary>
public readonly record struct SandboxProfileId
{
    /// <summary>Initializes a sandbox profile identity.</summary>
    /// <param name="value">The stable non-blank profile key.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public SandboxProfileId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable profile key.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
