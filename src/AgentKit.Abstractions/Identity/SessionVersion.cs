// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// An optimistic-concurrency version number for one session branch,
/// incremented every time a durable append changes that branch's history.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural, numeric equality
/// and ordering over <see cref="Value"/>. It carries no mutable state itself
/// and is safe to share and compare across threads without synchronization.
/// </para>
/// <para>
/// A version number is ordering data, not identity: unlike
/// <see cref="SessionId"/> or <see cref="BranchId"/>, two
/// <see cref="SessionVersion"/> values are not "the same version" merely
/// because they hold equal numbers — they only mean the same thing when
/// compared within the same session and branch. Session writers use the
/// version a reader last observed to detect a concurrent modification
/// before appending, so two writers racing to extend the same branch cannot
/// silently overwrite each other's messages.
/// </para>
/// <para>
/// Unlike the GUID-based identities, zero is a legitimate
/// <see cref="SessionVersion"/> (typically the version of a freshly created,
/// still-empty branch); only negative values are rejected.
/// </para>
/// </remarks>
public readonly record struct SessionVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionVersion"/>
    /// struct, validating that it is not a nonsensical negative version
    /// number.
    /// </summary>
    /// <param name="value">The non-negative version number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public SessionVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the version number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the version number as invariant-culture text, suitable for
    /// logging and optimistic-concurrency conflict messages.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
