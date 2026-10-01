// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Binds the store a captured goal profile names from the container's keyed registrations.</summary>
/// <remarks>Selection is by the profile's store key, never by registration order. The selector is the composition boundary that resolves keyed stores; runtime services receive this selector and never a service provider. Stores are borrowed, not owned.</remarks>
/// <param name="services">The root provider keyed stores are resolved from.</param>
/// <param name="profiles">The profile catalog resolving captured references.</param>
internal sealed class DefaultGoalStoreSelector(IServiceProvider services, IGoalProfileCatalog profiles): IGoalStoreSelector
{
    /// <inheritdoc/>
    public ValueTask<GoalStoreSelectionResult> SelectAsync(GoalStoreSelectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!profiles.TryGet(request.Profile, out var profile))
        {
            return ValueTask.FromResult<GoalStoreSelectionResult>(
                new GoalStoreSelectionRejected("No goal profile is published for the captured key and version."));
        }

        var store = services.GetKeyedService<IGoalStore>(profile.StoreKey.Value);
        return ValueTask.FromResult<GoalStoreSelectionResult>(store is null
            ? new GoalStoreSelectionRejected("No goal store is registered for the profile's store key.")
            : new GoalStoreSelected(profile.StoreKey, store));
    }
}
