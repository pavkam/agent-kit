// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates that every agent definition selecting an artifact coordinator resolves a complete, explicitly registered artifact composition.</summary>
/// <remarks>
/// Validation is additive and content-free: it inspects build-local registration evidence and the coordinator catalog, never
/// resolves a coordinator or store, and performs no I/O. An agent without an artifact coordinator produces no durable artifacts and
/// is not inspected. A coordinator replaced by an application type has no catalog evidence, so only its keyed registration is required.
/// </remarks>
internal static class ArtifactCompositionValidator
{
    /// <summary>Appends a diagnostic for every missing coordinator, profile, store, or engine service a selected coordinator needs.</summary>
    /// <param name="definitions">The materialized definition set.</param>
    /// <param name="catalog">The coordinator catalog, or null when none is registered.</param>
    /// <param name="catalogFailure">The safe message of a catalog-construction failure, or null when the catalog was built.</param>
    /// <param name="componentRegistrations">The build-local registration evidence.</param>
    /// <param name="diagnostics">The caller-owned diagnostic builder.</param>
    internal static void Validate(
        ImmutableArray<AgentDefinition> definitions,
        IArtifactCoordinatorCatalog? catalog,
        string? catalogFailure,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!definitions.IsDefault, "Artifact validation runs only over a materialized definition set.");
        Debug.Assert(componentRegistrations is not null, "Artifact validation requires registration evidence.");
        Debug.Assert(diagnostics is not null, "Artifact validation appends to a caller-owned builder.");
        var validatedEngineWide = false;
        var reportedCatalogFailure = false;
        var validated = new HashSet<ComponentKey<IArtifactCoordinator>>();
        foreach (var definition in definitions)
        {
            if (definition.OptionalCapabilities.ArtifactCoordinator is not { } key)
            {
                continue;
            }

            if (!validatedEngineWide)
            {
                RequireUnkeyed<ISecurityAuthoritySelector>(componentRegistrations, "agentkit.artifact.security-authority-selector.missing", diagnostics);
                RequireUnkeyed<TimeProvider>(componentRegistrations, "agentkit.artifact.time-provider.missing", diagnostics);
                validatedEngineWide = true;
            }

            if (!validated.Add(key))
            {
                continue;
            }

            if (!HasKeyed<IArtifactCoordinator>(componentRegistrations, key.Value))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.artifact.coordinator.missing",
                    $"Agent '{definition.Id}' selects artifact coordinator '{key.Value}' but no keyed IArtifactCoordinator is registered for it. Call AddAgentArtifacts."));
                continue;
            }

            if (catalogFailure is not null)
            {
                if (!reportedCatalogFailure)
                {
                    diagnostics.Add(new CompositionDiagnostic("agentkit.artifact.profile.invalid", catalogFailure));
                    reportedCatalogFailure = true;
                }

                continue;
            }

            if (catalog is null || !catalog.TryGet(key, out var snapshot))
            {
                continue;
            }

            foreach (var backend in snapshot.Backends)
            {
                if (!HasKeyed<IArtifactStore>(componentRegistrations, backend.Value))
                {
                    diagnostics.Add(new CompositionDiagnostic(
                        "agentkit.artifact.store.missing",
                        $"Artifact coordinator '{key.Value}' (profile '{snapshot.ProfileKey.Value}') routes to backend '{backend.Value}' but no keyed IArtifactStore is registered for it."));
                }
            }
        }
    }

    private static void RequireUnkeyed<TService>(ComponentRegistrationSnapshot componentRegistrations, string code, ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var registered = componentRegistrations.Services.Any(static service => !service.IsKeyedService && service.ServiceType == typeof(TService));
        if (!registered)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"An artifact coordinator is selected but no {typeof(TService).Name} is registered. Register the required engine services."));
        }
    }

    private static bool HasKeyed<TService>(ComponentRegistrationSnapshot componentRegistrations, string key)
        where TService : class =>
        componentRegistrations.Services.Any(service =>
            service.IsKeyedService
            && service.ServiceType == typeof(TService)
            && key.Equals(service.ServiceKey as string, StringComparison.Ordinal));
}
