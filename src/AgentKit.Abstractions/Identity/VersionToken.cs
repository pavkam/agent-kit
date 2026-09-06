// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An opaque optimistic-concurrency token for a versioned resource, used to
/// detect that a resource changed between when it was read and when an
/// update against it is attempted.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Unlike <see cref="SessionVersion"/> and <see cref="SessionSequence"/>,
/// which are AgentKit's own strictly ordered, numeric version and sequence
/// values for session history, <see cref="VersionToken"/> is intentionally
/// opaque: it exists to represent a storage backend's native concurrency
/// token (an ETag, a row version, a hybrid-logical-clock stamp) without
/// forcing every backend to fit AgentKit's own numeric scheme. Callers
/// compare tokens for equality to detect staleness; they must not attempt
/// to parse, order, or increment the token's contents.
/// </para>
/// </remarks>
public readonly record struct VersionToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionToken"/> struct,
    /// validating that it carries usable token text.
    /// </summary>
    /// <param name="value">The non-empty canonical version token text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public VersionToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical version token text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical token text. Because a version token is opaque,
    /// this text has meaning only to the storage backend that produced it.
    /// </summary>
    public override string ToString() => Value;
}
