// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one independently owned execution lane within a session.</summary>
/// <remarks>This immutable ordinal identity scopes operation ownership and queued input; it grants no authority.</remarks>
public readonly record struct ExecutionLaneId
{
    /// <summary>Initializes a lane identity.</summary>
    /// <param name="value">The nonempty stable lane value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public ExecutionLaneId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable lane value.</summary><value>The validated ordinal value.</value>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
