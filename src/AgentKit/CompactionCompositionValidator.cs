// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates that every agent selecting a compaction profile resolves to a published profile and a registered compactor.</summary>
/// <remarks>
/// <para>
/// Compaction is an optional capability. An agent that names no compaction profile is never diagnosed here, and a
/// composition with no such agent needs no profile catalog at all. Naming a profile states that this agent compacts
/// under a specific policy and compactor, so composition proves both exist rather than discovering a missing compactor
/// the first time a long conversation hits context pressure.
/// </para>
/// <para>
/// The profile catalog is the one service this validator resolves. Building it is the composition boundary at which the
/// compaction package validates every registered profile against its compactor and strategy registrations, so a
/// failure there is reported as a diagnostic carrying the catalog's own message. The compactor itself is checked
/// through build-local registration descriptors and is never activated, because activating a compactor during
/// validation would open a session store the composition has not yet been proven able to authorize.
/// </para>
/// </remarks>
internal static class CompactionCompositionValidator
{
    /// <summary>Diagnoses every compaction gap across the definitions that select a compaction profile.</summary>
    /// <param name="definitions">The published agent definitions this composition can run.</param>
    /// <param name="provider">The composed provider the profile catalog is resolved from.</param>
    /// <param name="componentRegistrations">The non-null build-local registration evidence.</param>
    /// <param name="diagnostics">The accumulating diagnostic builder this validator appends to.</param>
    /// <remarks>
    /// Each definition reports at most one diagnostic: without a published profile there is no compactor key to check,
    /// and a profile whose catalog failed to build would repeat the same message for every definition that names it.
    /// </remarks>
    internal static void Validate(
        ImmutableArray<AgentDefinition> definitions,
        IServiceProvider provider,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!definitions.IsDefault, "Compaction validation runs only over a materialized definition set.");
        Debug.Assert(provider is not null, "Compaction validation resolves the profile catalog from the composed provider.");
        Debug.Assert(componentRegistrations is not null, "Compaction validation requires registration evidence.");
        Debug.Assert(diagnostics is not null, "Compaction validation appends to a caller-owned builder.");

        var selecting = definitions.Where(static definition => definition.OptionalCapabilities.CompactionProfile is not null).ToArray();
        if (selecting.Length == 0)
        {
            return;
        }

        ICompactionProfileCatalog? catalog = null;
        try
        {
            catalog = provider.GetService<ICompactionProfileCatalog>();
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.compaction.profile-invalid",
                $"The compaction profile catalog could not be built: {exception.Message}"));
            return;
        }

        var checkedCompactors = new HashSet<ComponentKey<ICompactor>>();
        foreach (var definition in selecting)
        {
            var profileKey = definition.OptionalCapabilities.CompactionProfile!.Value;
            if (catalog is null || !catalog.TryGet(profileKey, out var publication))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.compaction.missing",
                    $"Agent '{definition.Id}' selects compaction profile '{profileKey.Value}' but it is not registered. Call AddCompactionProfile."));
                continue;
            }

            if (!publication.Enabled || !checkedCompactors.Add(publication.CompactorKey))
            {
                continue;
            }

            var key = publication.CompactorKey.Value;
            var registered = componentRegistrations.Services.Any(service =>
                service.IsKeyedService
                && service.ServiceType == typeof(ICompactor)
                && key.Equals(service.ServiceKey as string, StringComparison.Ordinal));
            if (!registered)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.compaction.compactor-missing",
                    $"Agent '{definition.Id}' selects compaction profile '{profileKey.Value}', which names compactor key '{key}', but no keyed {nameof(ICompactor)} registration exists for it."));
            }
        }
    }
}
