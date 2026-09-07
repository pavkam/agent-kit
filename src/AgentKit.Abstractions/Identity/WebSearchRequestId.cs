// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one bounded web-search attempt across authorization and provider execution.</summary>
public readonly record struct WebSearchRequestId
{
    /// <summary>Initializes a non-empty search request identity.</summary>
    /// <param name="value">The globally unique request value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public WebSearchRequestId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique request value.</summary>
    public Guid Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
