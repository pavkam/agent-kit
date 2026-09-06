// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content-addressable hash used to detect duplicate or tampered content,
/// such as the bytes referenced by a <see cref="MediaReference"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// <see cref="ContentHash"/> is deliberately algorithm-agnostic at the type
/// level: it stores whatever canonical hash text the producing component
/// chose to compute (for example a hex- or base64-encoded digest), and
/// callers that need to verify or recompute a hash must know, out of band,
/// which algorithm and encoding a given value uses. Two
/// <see cref="ContentHash"/> values are only meaningfully comparable when
/// they were produced by the same algorithm and encoding.
/// </para>
/// </remarks>
public readonly record struct ContentHash
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentHash"/> struct,
    /// validating that it carries usable hash text.
    /// </summary>
    /// <param name="value">The non-empty canonical hash text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ContentHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical hash text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical hash text.</summary>
    public override string ToString() => Value;
}
