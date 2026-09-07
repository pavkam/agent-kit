// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// The published revision of one <see cref="DurabilityProfileKey"/>'s
/// validated configuration, captured on durable records so that recovery
/// rebinds the profile the operation actually started under.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// Durability configuration is captured, not monitored. A profile change
/// produces a new version through validated publication rather than mutating
/// the settings an in-flight operation began with. Recovery uses the version
/// persisted on the record; substituting the agent's current profile would
/// silently reinterpret work that was journaled under different retry,
/// checkpoint, or fencing rules.
/// </para>
/// </remarks>
public readonly record struct DurabilityProfileVersion
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurabilityProfileVersion"/> struct, validating that it is
    /// not a nonsensical negative revision number.
    /// </summary>
    /// <param name="value">The non-negative profile revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public DurabilityProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the profile revision number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the revision number as invariant-culture text, suitable for
    /// logging and recovery-compatibility messages.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
