// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one committed revision of a session-directory location binding.</summary>
/// <remarks>The revision is routing evidence for one location record. It is not a session version and cannot authorize access or prove that the referenced store record exists.</remarks>
public readonly record struct SessionDirectoryRevision
{
    /// <summary>Initializes a committed directory revision.</summary>
    /// <param name="value">The positive committed revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public SessionDirectoryRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive committed revision number.</summary>
    /// <value>The recorded directory revision, or zero only for an unvalidated default value.</value>
    public long Value { get; }

    /// <summary>Returns the invariant-culture revision text.</summary>
    /// <returns>The numeric revision formatted with invariant culture.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
