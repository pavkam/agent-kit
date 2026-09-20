// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>One DI-registered hook implementation bound to one hook point within one profile.</summary>
/// <remarks>
/// <c>Add*Hook&lt;T&gt;</c> emits one binding alongside the hook service registration. Catalog discovery resolves
/// the live hook instance, projects its <see cref="IHook"/> ordering metadata into a
/// <see cref="HookRegistrationDescriptor"/>, and hands the descriptor to <see cref="HookRegistrationCatalog"/>.
/// </remarks>
internal sealed record HookRegistrationBinding
{
    /// <summary>Initializes a new instance of the <see cref="HookRegistrationBinding"/> record.</summary>
    /// <param name="point">The hook point this implementation targets.</param>
    /// <param name="profileKey">The profile this binding belongs to.</param>
    /// <param name="implementationType">The concrete hook implementation type.</param>
    /// <param name="hookServiceType">The closed hook interface used to resolve the implementation from DI.</param>
    /// <param name="lifetime">The activation lifetime declared for the implementation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is default or <paramref name="lifetime"/> is not a defined value.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profileKey"/> is null, or <paramref name="implementationType"/> or
    /// <paramref name="hookServiceType"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="profileKey"/> is blank, <paramref name="hookServiceType"/> is not an interface, or
    /// <paramref name="implementationType"/> does not implement <paramref name="hookServiceType"/>.
    /// </exception>
    public HookRegistrationBinding(
        HookPointId point,
        HookProfileKey profileKey,
        Type implementationType,
        Type hookServiceType,
        HookLifetime lifetime)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(point, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentNullException.ThrowIfNull(implementationType);
        ArgumentNullException.ThrowIfNull(hookServiceType);
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        if (!hookServiceType.IsInterface)
        {
            throw new ArgumentException("Hook service type must be an interface.", nameof(hookServiceType));
        }

        if (!hookServiceType.IsAssignableFrom(implementationType))
        {
            throw new ArgumentException("Implementation type must implement the hook service interface.", nameof(implementationType));
        }

        Point = point;
        ProfileKey = profileKey;
        ImplementationType = implementationType;
        HookServiceType = hookServiceType;
        Lifetime = lifetime;
    }

    /// <summary>Gets the hook point this implementation targets.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets the profile this binding belongs to.</summary>
    public HookProfileKey ProfileKey { get; }

    /// <summary>Gets the concrete hook implementation type.</summary>
    public Type ImplementationType { get; }

    /// <summary>Gets the closed hook interface used to resolve the implementation from DI.</summary>
    public Type HookServiceType { get; }

    /// <summary>Gets the activation lifetime declared for the implementation.</summary>
    public HookLifetime Lifetime { get; }

    /// <summary>Builds one descriptor from one resolved hook instance.</summary>
    /// <param name="hook">The resolved hook instance.</param>
    /// <param name="pointRegistration">The closed point metadata for <see cref="Point"/>.</param>
    /// <returns>The captured registration descriptor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> or <paramref name="pointRegistration"/> is null.</exception>
    public HookRegistrationDescriptor ToDescriptor(IHook hook, HookPointDefinitionRegistration pointRegistration)
    {
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(pointRegistration);
        ArgumentException.ThrowIfNotEqual(pointRegistration.Point, Point);

        return new HookRegistrationDescriptor(
            HookRegistrationIds.FromAuthorHookId(hook.Id),
            Point,
            ProfileKey,
            MapOrder(hook.Priority),
            Lifetime,
            pointRegistration.FailureInvariant,
            HookReentrancyPolicy.Forbidden,
            HookRegistrationIds.FromAuthorHookIds(hook.RunsBefore),
            HookRegistrationIds.FromAuthorHookIds(hook.RunsAfter),
            HookRegistrationIds.FromAuthorHookIds(hook.DependsOn));
    }

    private static HookOrder MapOrder(HookPriority priority) =>
        priority switch
        {
            HookPriority.First => HookOrder.First,
            HookPriority.Last => HookOrder.Last,
            HookPriority.Normal => HookOrder.Normal,
            _ => throw new InvalidOperationException($"Hook priority '{priority}' is not supported."),
        };
}
