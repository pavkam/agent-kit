// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// A monotonically increasing ownership generation issued with an execution
/// lease, used to reject durable writes from a worker whose lease has been
/// taken over.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// Fencing tokens are allocated atomically by the lease store, never by an
/// in-process counter, because their entire purpose is to order ownership
/// claims made by different processes that cannot see each other. Every
/// durable write performed under a lease carries its token, and the store
/// rejects a write whose token is older than the current authoritative one.
/// A renewal may extend a lease's expiry but never reuses an older token.
/// </para>
/// <para>
/// Ordering is defined so that a greater token always represents a later
/// ownership generation. <see cref="CompareTo(FencingToken)"/> and the
/// comparison operators exist specifically to express that staleness check
/// without callers reaching into <see cref="Value"/> and reimplementing it.
/// </para>
/// </remarks>
public readonly record struct FencingToken: IComparable<FencingToken>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FencingToken"/> struct,
    /// validating that it is a real allocated generation.
    /// </summary>
    /// <param name="value">
    /// The positive ownership generation allocated by the lease store.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is zero or negative. Zero is the default
    /// value and never identifies an allocated ownership generation, so
    /// accepting it would let an unfenced write masquerade as the oldest
    /// valid owner.
    /// </exception>
    public FencingToken(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the underlying ownership generation.</summary>
    public long Value { get; }

    /// <summary>
    /// Determines whether <paramref name="left"/> is an older ownership
    /// generation than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The token to test for staleness.</param>
    /// <param name="right">The token to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="left"/> was allocated
    /// before <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator <(FencingToken left, FencingToken right) =>
        left.Value < right.Value;

    /// <summary>
    /// Determines whether <paramref name="left"/> is a newer ownership
    /// generation than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The token to test.</param>
    /// <param name="right">The token to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="left"/> was allocated
    /// after <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator >(FencingToken left, FencingToken right) =>
        left.Value > right.Value;

    /// <summary>
    /// Determines whether <paramref name="left"/> is the same or an older
    /// ownership generation than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The token to test.</param>
    /// <param name="right">The token to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="left"/> is not newer than
    /// <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator <=(FencingToken left, FencingToken right) =>
        left.Value <= right.Value;

    /// <summary>
    /// Determines whether <paramref name="left"/> is the same or a newer
    /// ownership generation than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The token to test.</param>
    /// <param name="right">The token to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="left"/> is not older than
    /// <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator >=(FencingToken left, FencingToken right) =>
        left.Value >= right.Value;

    /// <summary>
    /// Compares this ownership generation with <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The token to compare against.</param>
    /// <returns>
    /// A negative value when this token is older than
    /// <paramref name="other"/>, zero when both represent the same
    /// generation, and a positive value when this token is newer.
    /// </returns>
    public int CompareTo(FencingToken other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Returns the ownership generation as invariant-culture text, suitable
    /// for logging and fencing-conflict messages.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
