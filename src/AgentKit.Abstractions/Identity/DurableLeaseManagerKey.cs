// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Selects one registered durable lease manager, which owns distributed
/// ownership state and allocates fencing tokens.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// Two workers only exclude each other when they contend on the same lease
/// manager. Recording the key on durable evidence makes that assumption
/// explicit and lets validation reject a composition in which one worker
/// leases through a different store than another and both believe they are
/// the authoritative owner.
/// </para>
/// </remarks>
public readonly record struct DurableLeaseManagerKey
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableLeaseManagerKey"/> struct, validating that it
    /// carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical lease manager key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableLeaseManagerKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical lease manager key text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical lease manager key text, suitable for logging and
    /// composition-validation messages.
    /// </summary>
    public override string ToString() => Value;
}
