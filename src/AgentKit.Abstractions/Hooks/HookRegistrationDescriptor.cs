// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One configured hook registration for one hook point within one profile, before catalog capture.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. Whoever registers a hook (an application, or a feature's own <c>Add*Hook&lt;T&gt;</c>
/// extension) supplies this descriptor once; <see cref="IHookRegistrationSource"/> and
/// <see cref="IHookCatalog"/> capture it unchanged into a <see cref="HookCatalogSnapshot"/>.
/// </para>
/// <para>
/// <see cref="Before"/> and <see cref="After"/> are soft ordering edges: they add a constraint only when the named
/// registration exists in the same point, profile, and catalog resolution and are otherwise ignored.
/// <see cref="DependsOn"/> is a hard relation: a named registration that is absent from that same resolution scope
/// is a composition failure. Reusing the same <see cref="Id"/> within the same point, profile, and catalog is also
/// a composition failure unless an explicit replacement registration API names it.
/// </para>
/// </remarks>
public sealed record HookRegistrationDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="HookRegistrationDescriptor"/> record.</summary>
    /// <param name="id">This registration's stable identity, unique within its point, profile, and catalog.</param>
    /// <param name="point">The hook point this registration targets.</param>
    /// <param name="profileKey">The hook profile this registration belongs to.</param>
    /// <param name="order">The requested coarse ordering anchor.</param>
    /// <param name="lifetime">The activation lifetime this registration's implementation declares.</param>
    /// <param name="requestedFailureMode">The failure mode this registration requests, subject to monotonic tightening.</param>
    /// <param name="reentrancy">Whether this registration may be re-entered by a nested dispatch of the same point.</param>
    /// <param name="before">The registrations, in the same resolution scope, that must run after this one when present.</param>
    /// <param name="after">The registrations, in the same resolution scope, that must run before this one when present.</param>
    /// <param name="dependsOn">The registrations that must be present in the same resolution scope and must run before this one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> or <paramref name="point"/> is default, <paramref name="lifetime"/> or
    /// <paramref name="requestedFailureMode"/> or <paramref name="reentrancy"/> is not a defined value.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> is default (blank).</exception>
    /// <exception cref="ArgumentNullException"><paramref name="order"/> is null.</exception>
    public HookRegistrationDescriptor(
        HookRegistrationId id,
        HookPointId point,
        HookProfileKey profileKey,
        HookOrder order,
        HookLifetime lifetime,
        HookFailureMode requestedFailureMode,
        HookReentrancyPolicy reentrancy,
        ImmutableArray<HookRegistrationId> before,
        ImmutableArray<HookRegistrationId> after,
        ImmutableArray<HookRegistrationId> dependsOn)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(point, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentNullException.ThrowIfNull(order);
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        ArgumentOutOfRangeException.ThrowIfUndefined(requestedFailureMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(reentrancy);

        Id = id;
        Point = point;
        ProfileKey = profileKey;
        Order = order;
        Lifetime = lifetime;
        RequestedFailureMode = requestedFailureMode;
        Reentrancy = reentrancy;
        Before = before;
        After = after;
        DependsOn = dependsOn;
    }

    /// <summary>Gets this registration's stable identity.</summary>
    public HookRegistrationId Id { get; }

    /// <summary>Gets the hook point this registration targets.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets the hook profile this registration belongs to.</summary>
    public HookProfileKey ProfileKey { get; }

    /// <summary>Gets the requested coarse ordering anchor.</summary>
    public HookOrder Order { get; }

    /// <summary>Gets the activation lifetime this registration's implementation declares.</summary>
    public HookLifetime Lifetime { get; }

    /// <summary>Gets the failure mode this registration requests, subject to monotonic tightening.</summary>
    public HookFailureMode RequestedFailureMode { get; }

    /// <summary>Gets whether this registration may be re-entered by a nested dispatch of the same point.</summary>
    public HookReentrancyPolicy Reentrancy { get; }

    /// <summary>Gets the registrations that must run after this one, when present in the same resolution scope.</summary>
    public ImmutableArray<HookRegistrationId> Before { get; }

    /// <summary>Gets the registrations that must run before this one, when present in the same resolution scope.</summary>
    public ImmutableArray<HookRegistrationId> After { get; }

    /// <summary>Gets the registrations that must be present in the same resolution scope and must run before this one.</summary>
    public ImmutableArray<HookRegistrationId> DependsOn { get; }

    /// <inheritdoc/>
    public bool Equals(HookRegistrationDescriptor? other) =>
        other is not null
        && Id.Equals(other.Id)
        && Point.Equals(other.Point)
        && ProfileKey.Equals(other.ProfileKey)
        && Order.Equals(other.Order)
        && Lifetime == other.Lifetime
        && RequestedFailureMode == other.RequestedFailureMode
        && Reentrancy == other.Reentrancy
        && Before.SequenceEqual(other.Before)
        && After.SequenceEqual(other.After)
        && DependsOn.SequenceEqual(other.DependsOn);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Point);
        hash.Add(ProfileKey);
        hash.Add(Order);
        hash.Add(Lifetime);
        hash.Add(RequestedFailureMode);
        hash.Add(Reentrancy);
        foreach (var id in Before)
        {
            hash.Add(id);
        }

        foreach (var id in After)
        {
            hash.Add(id);
        }

        foreach (var id in DependsOn)
        {
            hash.Add(id);
        }

        return hash.ToHashCode();
    }
}
