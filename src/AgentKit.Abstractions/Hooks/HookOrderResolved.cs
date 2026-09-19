// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the deterministic dispatch order for one point's registrations.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="Ordered"/> preserves every soft <c>Before</c>/<c>After</c> constraint, every
/// hard <c>DependsOn</c> edge, at most one <see cref="HookOrderAnchor.First"/> and one
/// <see cref="HookOrderAnchor.Last"/> registration, and falls back to registration-discovery order as the final
/// tie-breaker.
/// </remarks>
public sealed record HookOrderResolved: HookOrderResult
{
    /// <summary>Initializes a successful ordering.</summary>
    /// <param name="ordered">The registrations in deterministic dispatch order.</param>
    /// <exception cref="ArgumentException"><paramref name="ordered"/> is default or contains a null element.</exception>
    public HookOrderResolved(ImmutableArray<HookRegistrationDescriptor> ordered)
    {
        ArgumentException.ThrowIfContainsNull(ordered, nameof(ordered));
        Ordered = ordered;
    }

    /// <summary>Gets the registrations in deterministic dispatch order.</summary>
    public ImmutableArray<HookRegistrationDescriptor> Ordered { get; }

    /// <inheritdoc/>
    public bool Equals(HookOrderResolved? other) => other is not null && Ordered.SequenceEqual(other.Ordered);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var registration in Ordered)
        {
            hash.Add(registration);
        }

        return hash.ToHashCode();
    }
}
