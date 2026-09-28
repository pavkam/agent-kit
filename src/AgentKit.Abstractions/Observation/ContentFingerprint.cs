// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries a content-addressable fingerprint for redacted or omitted observation payloads.</summary>
/// <remarks>The fingerprint text is algorithm-agnostic at the type level; producers and consumers must agree out of band on encoding and hashing.</remarks>
public readonly record struct ContentFingerprint
{
    /// <summary>Initializes a validated fingerprint.</summary>
    /// <param name="value">The non-empty canonical fingerprint text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or consists only of whitespace.</exception>
    public ContentFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical fingerprint text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
