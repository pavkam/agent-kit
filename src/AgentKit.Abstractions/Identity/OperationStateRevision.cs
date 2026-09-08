// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies a positive revision of one execution lane's installed operation state.</summary>
/// <remarks>Equality is value-based. The revision is domain concurrency evidence for the named lane operation, independent of backend version tokens and unrelated session appends; it is not a session-wide ordering sequence.</remarks>
public readonly record struct OperationStateRevision
{
    /// <summary>Initializes an operation-state revision.</summary>
    /// <param name="value">The positive revision number assigned by the operation-state owner.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public OperationStateRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive operation-state revision number.</summary>
    /// <value>A value greater than zero used to invalidate proposals that observed a changed operation state.</value>
    public long Value { get; }

    /// <summary>Formats the numeric revision using invariant culture.</summary>
    /// <returns>The decimal representation of <see cref="Value"/> using invariant culture.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
