// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable effective security-policy snapshot.</summary>
/// <remarks>The identifier names captured policy evidence; possessing it does not grant authority. A default value is uninitialized and must be rejected by consumers.</remarks>
public readonly record struct SecurityPolicySnapshotId
{
    /// <summary>Initializes a policy-snapshot identity.</summary>
    /// <param name="value">The nonempty globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is <see cref="Guid.Empty"/>.</exception>
    public SecurityPolicySnapshotId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    /// <value>The snapshot identity, or <see cref="Guid.Empty"/> for a default instance.</value>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase hyphenated identifier text.</summary>
    /// <returns>The GUID in <c>D</c> format.</returns>
    public override string ToString() => Value.ToString("D");
}
