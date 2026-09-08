// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one independently owned execution lane within a session.</summary>
/// <remarks>This immutable ordinal value scopes active-operation ownership and pending input to one lane. Equality is exact ordinal string equality; the identity grants neither authority nor access to another lane's work.</remarks>
public readonly record struct ExecutionLaneId
{
    /// <summary>Initializes an execution-lane identity.</summary>
    /// <param name="value">The non-null, non-whitespace stable lane value, compared using exact ordinal string semantics.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public ExecutionLaneId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable lane value.</summary>
    /// <value>A non-empty ordinal string used as the lane identity; casing and other textual differences remain distinct.</value>
    public string Value { get; }

    /// <summary>Returns the exact lane identity text.</summary>
    /// <returns>The validated ordinal <see cref="Value"/> without normalization or formatting changes.</returns>
    public override string ToString() => Value;
}
