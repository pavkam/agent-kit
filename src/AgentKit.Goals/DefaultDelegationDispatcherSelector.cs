// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Binds the dispatcher a captured goal profile names from the container's keyed registrations.</summary>
/// <param name="services">The root provider keyed dispatchers are resolved from.</param>
/// <param name="profiles">The profile catalog resolving captured references.</param>
internal sealed class DefaultDelegationDispatcherSelector(IServiceProvider services, IGoalProfileCatalog profiles): IDelegationDispatcherSelector
{
    /// <inheritdoc/>
    public ValueTask<DelegationDispatcherSelectionResult> SelectAsync(DelegationDispatcherSelectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!profiles.TryGet(request.Profile, out var profile))
        {
            return ValueTask.FromResult<DelegationDispatcherSelectionResult>(
                new DelegationDispatcherSelectionRejected("No goal profile is published for the captured key and version."));
        }

        var dispatcher = services.GetKeyedService<IDelegationDispatcher>(profile.DispatcherKey.Value);
        return ValueTask.FromResult<DelegationDispatcherSelectionResult>(dispatcher is null
            ? new DelegationDispatcherSelectionRejected("No delegation dispatcher is registered for the profile's dispatcher key.")
            : new DelegationDispatcherSelected(profile.DispatcherKey, dispatcher));
    }
}
