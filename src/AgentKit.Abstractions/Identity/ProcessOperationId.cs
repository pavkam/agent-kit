// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one immutable attempt to execute a host process.</summary>
public readonly record struct ProcessOperationId
{
    /// <summary>Initializes a non-empty process-operation identity.</summary>
    /// <param name="value">The globally unique operation value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public ProcessOperationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the underlying globally unique value.</summary>
    public Guid Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
