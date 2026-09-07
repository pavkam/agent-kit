// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

/// <summary>Identifies one host-configured resource independently from its backing path.</summary>
public readonly record struct ResourceId
{
    /// <summary>Initializes a validated resource identity.</summary>
    /// <param name="value">The non-blank stable identity.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ResourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable identity.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
