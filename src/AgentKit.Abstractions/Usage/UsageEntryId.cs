// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one charged invocation's usage across reports and accounting corrections.</summary>
/// <remarks>Retries receive distinct entry identities even when their logical request and operation identities remain unchanged. Corrections retain the original entry identity. Producers allocate identities through an injected identifier generator.</remarks>
public readonly record struct UsageEntryId
{
    /// <summary>Captures a nonempty existing or generated usage identity.</summary>
    /// <param name="value">The nonempty globally unique identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    [System.Text.Json.Serialization.JsonConstructor]
    public UsageEntryId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the immutable identity used to correlate revisions of one contribution.</summary>
    /// <value>A nonempty GUID for constructed values; consumers must reject the CLR default.</value>
    public Guid Value { get; }

    /// <summary>Formats the identity for canonical storage and correlation.</summary>
    /// <returns>The lowercase, hyphenated GUID representation.</returns>
    public override string ToString() => Value.ToString("D");
}
