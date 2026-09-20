// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Captures one immutable hook catalog by discovering, validating, and merging every registration source.</summary>
public sealed class HookRegistrationCatalog: IHookCatalog
{
    private readonly IEnumerable<IHookRegistrationSource> _sources;
    private readonly IHookOrderResolver _orderResolver;
    private readonly Dictionary<HookPointId, HookPointDefinitionRegistration> _points;

    /// <summary>Initializes a new instance of the <see cref="HookRegistrationCatalog"/> class.</summary>
    /// <param name="sources">Every registration source registered in the composition.</param>
    /// <param name="orderResolver">The order resolver used to validate each point's registrations.</param>
    /// <param name="points">Every closed point definition registered in the composition.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sources"/>, <paramref name="orderResolver"/>, or <paramref name="points"/> is null.
    /// </exception>
    public HookRegistrationCatalog(
        IEnumerable<IHookRegistrationSource> sources,
        IHookOrderResolver orderResolver,
        IReadOnlyList<HookPointDefinitionRegistration> points)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(orderResolver);
        ArgumentNullException.ThrowIfNull(points);

        _sources = sources;
        _orderResolver = orderResolver;
        _points = points.ToDictionary(static point => point.Point);
    }

    /// <inheritdoc/>
    public async ValueTask<HookCatalogSnapshot> CaptureAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var discovered = ImmutableArray.CreateBuilder<HookRegistrationDescriptor>();
        foreach (var source in _sources)
        {
            var snapshot = await source.DiscoverAsync(request, cancellationToken).ConfigureAwait(false);
            discovered.AddRange(snapshot.Registrations);
        }

        ValidatePoints(discovered);
        ValidateDuplicates(discovered);
        ValidateOrdering(discovered);

        return new HookCatalogSnapshot(
            request.ProfileKey,
            new HookCatalogVersion(Guid.NewGuid().ToString("D")),
            discovered.ToImmutable());
    }

    private void ValidatePoints(ImmutableArray<HookRegistrationDescriptor>.Builder registrations)
    {
        foreach (var registration in registrations)
        {
            if (!_points.ContainsKey(registration.Point))
            {
                throw new HookCompositionException(
                    $"Hook registration '{registration.Id}' targets unknown point '{registration.Point}'.");
            }
        }
    }

    private static void ValidateDuplicates(ImmutableArray<HookRegistrationDescriptor>.Builder registrations)
    {
        var seen = new HashSet<(HookPointId Point, HookRegistrationId Id)>();
        foreach (var registration in registrations)
        {
            if (!seen.Add((registration.Point, registration.Id)))
            {
                throw new HookCompositionException(
                    $"Hook registration '{registration.Id}' is duplicated for point '{registration.Point}'.");
            }
        }
    }

    private void ValidateOrdering(ImmutableArray<HookRegistrationDescriptor>.Builder registrations)
    {
        var byPoint = registrations.ToLookup(static registration => registration.Point);
        foreach (var group in byPoint)
        {
            var result = _orderResolver.Resolve([.. group]);
            if (result is HookOrderInvalid invalid)
            {
                throw new HookCompositionException(string.Join("; ", invalid.Diagnostics));
            }
        }
    }
}
