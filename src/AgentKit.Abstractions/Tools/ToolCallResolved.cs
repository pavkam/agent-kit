// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Transfers a resolved call and its owned invoker lease to the caller.</summary>
/// <remarks>Result copies share the same lease reference; they never duplicate its ownership or extend its lifetime.</remarks>
public sealed record ToolCallResolved: ToolResolutionResult
{
    /// <summary>Wraps a resolved call together with the lease acquired for its exact descriptor.</summary>
    /// <param name="call">The nonnull resolved call.</param>
    /// <param name="lease">The nonnull invoker lease whose ownership transfers to the recipient; it must be released once the call settles.</param>
    /// <exception cref="ArgumentNullException"><paramref name="call"/> or <paramref name="lease"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="lease"/>'s descriptor does not match <paramref name="call"/>'s resolved tool.</exception>
    public ToolCallResolved(ResolvedToolCall call, IToolInvokerLease lease)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentException.ThrowIfNotEqual(lease.Tool, call.Tool, nameof(lease));
        Call = call;
        Lease = lease;
    }

    /// <summary>Gets the resolved call.</summary>
    /// <value>The exact descriptor, execution policy, and identity bound by resolution.</value>
    public ResolvedToolCall Call { get; }

    /// <summary>Gets the acquisition that the caller releases after the call settles.</summary>
    /// <value>The supplied lease, shared by any copies of this result.</value>
    public IToolInvokerLease Lease { get; }

    /// <summary>Compares the resolved call and lease identity without invoking equality on a third-party lease.</summary>
    /// <param name="other">The result to compare, or null.</param>
    /// <returns>True only when both results share the same resolved call and lease instance.</returns>
    public bool Equals(ToolCallResolved? other) =>
        other is not null && Call == other.Call && ReferenceEquals(Lease, other.Lease);

    /// <summary>Hashes the resolved call and the identity of the retained lease without invoking its implementation.</summary>
    /// <returns>A hash compatible with resolved-result equality.</returns>
    public override int GetHashCode() =>
        HashCode.Combine(Call, ReferenceEqualityComparer.Instance.GetHashCode(Lease));
}
