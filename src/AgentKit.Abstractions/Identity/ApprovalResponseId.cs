// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one authenticated resolution of a durable approval request.</summary>
public readonly record struct ApprovalResponseId
{
    /// <summary>Initializes a nondefault approval-response identity.</summary>
    /// <param name="value">The stable response value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public ApprovalResponseId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the stable response value.</summary>
    public Guid Value { get; }

    /// <summary>Formats the identity using the invariant compact GUID representation.</summary>
    /// <returns>The compact identity text.</returns>
    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}
