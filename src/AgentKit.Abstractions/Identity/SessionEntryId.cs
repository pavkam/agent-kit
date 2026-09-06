// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one immutable <see cref="SessionEntry"/> in a session's
/// append-only record.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization. Like every AgentKit identity struct, this constructor
/// rejects <see cref="Guid.Empty"/> so a valid instance always addresses a
/// real entry.
/// </remarks>
public readonly record struct SessionEntryId
{
    /// <summary>Initializes a new instance of the <see cref="SessionEntryId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public SessionEntryId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
