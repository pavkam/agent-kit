// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Discovers hook registrations emitted by first-party <c>Add*Hook&lt;T&gt;</c> registration bindings.</summary>
internal sealed class HookRegistrationBindingSource: IHookRegistrationSource
{
    private readonly HookRegistrationBindingRegistry _bindings;
    private readonly HookProfileRegistry _profiles;
    private readonly Dictionary<HookPointId, HookPointDefinitionRegistration> _points;

    /// <summary>Initializes a new instance of the <see cref="HookRegistrationBindingSource"/> class.</summary>
    /// <param name="bindings">Every registration binding registered in the composition.</param>
    /// <param name="profiles">The registered hook profiles used to apply optional registration filters.</param>
    /// <param name="points">Every closed point definition registered in the composition.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bindings"/>, <paramref name="profiles"/>, or <paramref name="points"/> is null.
    /// </exception>
    public HookRegistrationBindingSource(
        HookRegistrationBindingRegistry bindings,
        HookProfileRegistry profiles,
        IReadOnlyList<HookPointDefinitionRegistration> points)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(points);

        _bindings = bindings;
        _profiles = profiles;
        _points = HookPointDefinitionRegistrations.ToDictionary(points);
    }

    /// <inheritdoc/>
    public ValueTask<HookRegistrationSnapshot> DiscoverAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_bindings.Bindings.Count == 0)
        {
            return new ValueTask<HookRegistrationSnapshot>(new HookRegistrationSnapshot([]));
        }

        var profileOptions = _profiles.GetRequired(request.ProfileKey);
        var filter = profileOptions.RegistrationFilter;
        var registrations = ImmutableArray.CreateBuilder<HookRegistrationDescriptor>();
        var seenRegistrationIds = new HashSet<HookRegistrationId>();
        foreach (var binding in _bindings.Bindings)
        {
            if (!binding.Descriptor.ProfileKey.Equals(request.ProfileKey))
            {
                continue;
            }

            if (filter is not null && !filter(binding.Descriptor))
            {
                continue;
            }

            if (!seenRegistrationIds.Add(binding.Descriptor.Id))
            {
                throw new HookCompositionException(
                    $"Hook registration '{binding.Descriptor.Id}' is duplicated for point '{binding.Descriptor.Point}'.");
            }

            if (!_points.TryGetValue(binding.Descriptor.Point, out var pointRegistration))
            {
                throw new HookCompositionException(
                    $"Hook point '{binding.Descriptor.Point}' is not registered in the point-definition catalog.");
            }

            ArgumentException.ThrowIfNotEqual(pointRegistration.Point, binding.Descriptor.Point);
            registrations.Add(binding.Descriptor);
        }

        return new ValueTask<HookRegistrationSnapshot>(new HookRegistrationSnapshot(registrations.ToImmutable()));
    }
}
