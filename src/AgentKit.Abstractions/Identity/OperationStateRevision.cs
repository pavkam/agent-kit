// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies a positive revision of one execution lane's installed operation state.</summary>
/// <remarks>The revision is domain concurrency evidence and is independent of backend version tokens and unrelated session appends.</remarks>
public readonly record struct OperationStateRevision
{
    /// <summary>Initializes an operation-state revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public OperationStateRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive revision number.</summary><value>The validated revision.</value>
    public long Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
