// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Captures a tool's declared effect class and optional replay and resource evidence.</summary>
/// <remarks>
/// Declarations are untrusted policy inputs and never grant authority. A null idempotency classification or resource
/// collection means unasserted; an initialized empty resource collection explicitly asserts no protected resource kind.
/// </remarks>
public sealed record ToolEffects
{
    /// <summary>Initializes immutable tool effect declarations.</summary>
    /// <param name="effect">The defined coarse effect class.</param>
    /// <param name="idempotency">The optional defined replay-safety classification.</param>
    /// <param name="requiredResourceKinds">The optional initialized, unique resource kinds; null is unasserted and empty explicitly none.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="idempotency"/> claims read-only replay semantics for a mutating effect, or
    /// <paramref name="requiredResourceKinds"/> is a default array or contains duplicates.
    /// </exception>
    public ToolEffects(ToolEffect effect, IdempotencyClassification? idempotency, ImmutableArray<ProtectedResourceKind>? requiredResourceKinds)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        if (idempotency is { } replaySafety)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(replaySafety, nameof(idempotency));
        }

        ArgumentException.ThrowIfNotEqual(
            effect == ToolEffect.Mutating && idempotency == IdempotencyClassification.ReadOnly,
            false,
            nameof(idempotency));

        if (requiredResourceKinds is { } resources)
        {
            ArgumentException.ThrowIfDefault(resources, nameof(requiredResourceKinds));
            var unique = new HashSet<ProtectedResourceKind>();
            foreach (var resource in resources)
            {
                ArgumentOutOfRangeException.ThrowIfUndefined(resource, nameof(requiredResourceKinds));
                ArgumentException.ThrowIfNotEqual(unique.Add(resource), true, nameof(requiredResourceKinds));
            }
        }

        Effect = effect;
        Idempotency = idempotency;
        RequiredResourceKinds = requiredResourceKinds;
    }

    /// <summary>Gets the defined coarse effect class.</summary>
    /// <value>An untrusted declaration used by policy; it never authorizes the described effect.</value>
    public ToolEffect Effect { get; }
    /// <summary>Gets the replay-safety classification, or null when unasserted.</summary>
    /// <value>
    /// A conservative optional replay claim. A mutating tool can never claim
    /// <see cref="IdempotencyClassification.ReadOnly"/> because that value means no external mutation.
    /// </value>
    public IdempotencyClassification? Idempotency { get; }
    /// <summary>Gets the optional ordered resource-kind declaration.</summary>
    /// <value>
    /// Null when unasserted, an initialized empty array when explicitly none,
    /// or unique defined kinds. These declarations never grant access to a concrete resource.
    /// </value>
    public ImmutableArray<ProtectedResourceKind>? RequiredResourceKinds { get; }

    /// <summary>Compares every declaration, including resource order and the unasserted-versus-empty distinction.</summary>
    /// <param name="other">The declaration to compare, or null.</param>
    /// <returns>True when all declaration evidence is equal.</returns>
    public bool Equals(ToolEffects? other) =>
        other is not null && Effect == other.Effect && Idempotency == other.Idempotency
        && NullableSequenceEqual(RequiredResourceKinds, other.RequiredResourceKinds);

    /// <summary>Returns a hash code consistent with declaration equality.</summary>
    /// <returns>A hash derived from asserted values in order.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Effect);
        hash.Add(Idempotency);
        if (RequiredResourceKinds is { } resources)
        {
            hash.Add(true);
            foreach (var resource in resources)
            {
                hash.Add(resource);
            }
        }

        return hash.ToHashCode();
    }

    private static bool NullableSequenceEqual(ImmutableArray<ProtectedResourceKind>? left, ImmutableArray<ProtectedResourceKind>? right)
    {
        Debug.Assert(left is null || !left.Value.IsDefault, "A present left declaration must be initialized.");
        Debug.Assert(right is null || !right.Value.IsDefault, "A present right declaration must be initialized.");
        return left is null ? right is null : right is { } value && left.Value.SequenceEqual(value);
    }
}
