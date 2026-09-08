// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable security audit record independently of log or trace identifiers.</summary>
public readonly record struct SecurityAuditRecordId
{
    /// <summary>Initializes a non-empty audit-record identity.</summary>
    /// <param name="value">The stable non-empty identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public SecurityAuditRecordId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }
    /// <summary>Gets the stable identity value.</summary>
    public Guid Value { get; }
    /// <summary>Returns canonical identity text.</summary>
    public override string ToString() => Value.ToString("D");
}
