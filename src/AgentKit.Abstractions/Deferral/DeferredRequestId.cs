// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one durable deferred request across handoff, resolution and continuation.</summary>
/// <remarks>Producers allocate this identity through an injected identifier generator. Replays and resolutions retain it; it is neither an approval nor permission to repeat an effect.</remarks>
public readonly record struct DeferredRequestId
{
    /// <summary>Captures an existing or newly generated nonempty request identity.</summary>
    /// <param name="value">The nonempty globally unique value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is empty.</exception>
    [System.Text.Json.Serialization.JsonConstructor]
    public DeferredRequestId(Guid value) { ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty); Value = value; }
    /// <summary>Gets the stable correlation value.</summary>
    /// <value>A nonempty GUID for constructed instances; consuming boundaries reject the CLR default.</value>
    public Guid Value { get; }
    /// <summary>Formats the request identity for canonical correlation.</summary>
    /// <returns>The lowercase hyphenated GUID representation.</returns>
    public override string ToString() => Value.ToString("D");
}
