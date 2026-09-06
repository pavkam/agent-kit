// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one registered <see cref="ISessionStore"/> implementation,
/// such as an in-memory or SQLite-backed store.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>, safe to share across threads
/// without synchronization. A session's <see cref="SessionDescriptor"/>
/// records the store key it was created with as an integrity check, so a
/// later misconfiguration cannot silently route the same session to a
/// different store.
/// </remarks>
public readonly record struct SessionStoreKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStoreKey"/>
    /// struct, validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical store key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public SessionStoreKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical store key text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical key text.</summary>
    public override string ToString() => Value;
}
