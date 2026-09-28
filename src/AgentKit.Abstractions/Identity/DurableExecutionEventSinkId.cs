// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one durable execution event sink registered with the durability event dispatcher.</summary>
/// <remarks>
/// The identity makes dispatch order, delivery requirements, and diagnostics attributable to an exact registration. It
/// is a registration name, not a durable operation or authorization identity, and grants no authority.
/// </remarks>
public readonly record struct DurableExecutionEventSinkId
{
    /// <summary>Initializes a validated <see cref="DurableExecutionEventSinkId"/> value.</summary>
    /// <param name="value">The non-empty canonical registration name.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public DurableExecutionEventSinkId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical registration name.</summary>
    /// <value>The non-empty text supplied at construction, or <see langword="null"/> for a default value.</value>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
