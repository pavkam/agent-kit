// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the retention policy selected by a session profile.</summary>
/// <remarks>This immutable ordinal key selects configuration only. It grants no deletion or archival authority, which remains separately authorized at the effecting store boundary.</remarks>
public readonly record struct SessionRetentionProfileKey
{
    /// <summary>Initializes a retention-policy selection key.</summary>
    /// <param name="value">The nonblank canonical key text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public SessionRetentionProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical selection key text.</summary>
    /// <value>Nonblank ordinal text preserved without normalization.</value>
    public string Value { get; }

    /// <summary>Returns the canonical selection key text.</summary>
    /// <returns>The immutable key text.</returns>
    public override string ToString() => Value;
}
