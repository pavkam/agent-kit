// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Transfers a successful invoker acquisition to its caller.</summary>
/// <remarks>Result copies share the same lease reference; they never duplicate its ownership or extend its lifetime.</remarks>
public sealed record ToolInvokerAcquired: ToolInvokerLeaseResult
{
    /// <summary>Wraps an acquired lease without reading live metadata or invoking its tool.</summary>
    /// <param name="lease">The nonnull lease whose ownership transfers to the recipient.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public ToolInvokerAcquired(IToolInvokerLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        Lease = lease;
    }

    /// <summary>Gets the acquisition that the caller releases after use.</summary>
    /// <value>The supplied lease, shared by any copies of this result.</value>
    public IToolInvokerLease Lease { get; }

    /// <summary>Compares acquisition identity without invoking equality on a third-party lease.</summary>
    /// <param name="other">The result to compare, or null.</param>
    /// <returns>True only when both results share the same lease instance; equivalent bindings do not merge ownership.</returns>
    public bool Equals(ToolInvokerAcquired? other) => other is not null && ReferenceEquals(Lease, other.Lease);

    /// <summary>Hashes the identity of the retained lease without invoking its implementation.</summary>
    /// <returns>A reference-identity hash compatible with acquired-result equality.</returns>
    public override int GetHashCode() => ReferenceEqualityComparer.Instance.GetHashCode(Lease);
}
