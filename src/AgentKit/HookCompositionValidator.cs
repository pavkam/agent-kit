// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates hook kernel services and default-profile catalog capture for one built composition.</summary>
internal static class HookCompositionValidator
{
    /// <summary>Collects hook readiness diagnostics without mutating the composition.</summary>
    /// <param name="provider">The built composition under validation.</param>
    /// <param name="diagnostics">The initialized diagnostic collector.</param>
    internal static void Validate(
        IServiceProvider provider,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Debug.Assert(diagnostics is not null, "Composition validation owns an initialized diagnostic collector.");

        ValidatePointDefinitions(provider, diagnostics);

        var catalog = Resolve<IHookCatalog>(provider, diagnostics, "agentkit.hook-catalog.missing");
        _ = Resolve<IHookDispatcher>(provider, diagnostics, "agentkit.hook-dispatcher.missing");
        _ = Resolve<IHookProfileSelector>(provider, diagnostics, "agentkit.hook-profile-selector.missing");
        _ = Resolve<IHookOrderResolver>(provider, diagnostics, "agentkit.hook-order-resolver.missing");
        _ = Resolve<IHookInstanceFactory>(provider, diagnostics, "agentkit.hook-instance-factory.missing");
        _ = Resolve<IIdentifierGenerator<HookDispatchId>>(provider, diagnostics, "agentkit.hook-dispatch-id.missing");
        _ = Resolve<IIdentifierGenerator<HookInvocationId>>(provider, diagnostics, "agentkit.hook-invocation-id.missing");

        if (catalog is null)
        {
            return;
        }

        try
        {
            _ = catalog
                .CaptureAsync(new HookCatalogRequest(HookRegistrationDescriptors.DefaultProfileKey), CancellationToken.None)
                .AsTask()
                .ConfigureAwait(continueOnCapturedContext: false)
                .GetAwaiter()
                .GetResult();
        }
        catch (HookCompositionException exception)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.hook-catalog.capture-failed",
                exception.Message));
        }
    }

    /// <summary>Validates that every published agent definition selects a resolvable hook profile.</summary>
    /// <param name="catalog">The agent definition catalog whose current snapshot is inspected.</param>
    /// <param name="profileSelector">The hook profile selector registered in the composition.</param>
    /// <param name="diagnostics">The initialized diagnostic collector.</param>
    internal static void ValidateDefinitionHookProfiles(
        IAgentDefinitionCatalog catalog,
        IHookProfileSelector profileSelector,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(profileSelector);
        Debug.Assert(diagnostics is not null, "Composition validation owns an initialized diagnostic collector.");

        var snapshot = catalog.CurrentSnapshot;
        if (snapshot is null)
        {
            return;
        }

        foreach (var definition in snapshot.Definitions)
        {
            var selection = profileSelector
                .SelectAsync(new HookProfileSelectionRequest(definition.HookProfile, definition.Id), CancellationToken.None)
                .AsTask()
                .ConfigureAwait(continueOnCapturedContext: false)
                .GetAwaiter()
                .GetResult();
            if (selection is HookProfileUnavailable unavailable)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.hook-profile.unavailable",
                    $"Agent '{definition.Id}' selects hook profile '{unavailable.RequestedProfile}' but it is not registered."));
            }
        }
    }

    /// <summary>Validates singular unkeyed hook kernel registrations from build-local metadata.</summary>
    /// <param name="snapshot">The frozen Microsoft DI registrations for this build.</param>
    /// <param name="diagnostics">The initialized diagnostic collector.</param>
    internal static void ValidateRegistrations(
        ComponentRegistrationSnapshot snapshot,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Debug.Assert(diagnostics is not null, "Composition validation owns an initialized diagnostic collector.");

        ValidateSingular<IHookDispatcher>(snapshot, diagnostics, "agentkit.hook-dispatcher");
        ValidateSingular<IHookCatalog>(snapshot, diagnostics, "agentkit.hook-catalog");
        ValidateSingular<IHookProfileSelector>(snapshot, diagnostics, "agentkit.hook-profile-selector");
        ValidateSingular<IHookOrderResolver>(snapshot, diagnostics, "agentkit.hook-order-resolver");
        ValidateSingular<IHookInstanceFactory>(snapshot, diagnostics, "agentkit.hook-instance-factory");
        ValidateSingular<IIdentifierGenerator<HookDispatchId>>(snapshot, diagnostics, "agentkit.hook-dispatch-id");
        ValidateSingular<IIdentifierGenerator<HookInvocationId>>(snapshot, diagnostics, "agentkit.hook-invocation-id");
        ValidateSingular<IReadOnlyList<HookPointDefinitionRegistration>>(snapshot, diagnostics, "agentkit.hook-point-definitions");
        ValidatePointDefinitionInstances(snapshot, diagnostics);
    }

    private static void ValidatePointDefinitionInstances(
        ComponentRegistrationSnapshot snapshot,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        foreach (var descriptor in snapshot.Services)
        {
            if (descriptor.IsKeyedService
                || descriptor.ServiceType != typeof(IReadOnlyList<HookPointDefinitionRegistration>))
            {
                continue;
            }

            if (descriptor.ImplementationInstance is not IReadOnlyList<HookPointDefinitionRegistration> registrations)
            {
                continue;
            }

            try
            {
                _ = HookPointDefinitionRegistrations.ToDictionary(registrations);
            }
            catch (HookCompositionException exception)
            {
                diagnostics.Add(new CompositionDiagnostic("agentkit.hook-point.collision", exception.Message));
                return;
            }
        }
    }

    private static void ValidatePointDefinitions(
        IServiceProvider provider,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        var lists = provider.GetServices<IReadOnlyList<HookPointDefinitionRegistration>>().ToArray();
        if (lists.Length != 1)
        {
            diagnostics.Add(new CompositionDiagnostic(
                lists.Length == 0
                    ? "agentkit.hook-point-definitions.missing"
                    : "agentkit.hook-point-definitions.ambiguous",
                lists.Length == 0
                    ? "No IReadOnlyList<HookPointDefinitionRegistration> is registered. Call AddAgentHooks on the service collection."
                    : $"Expected exactly one IReadOnlyList<HookPointDefinitionRegistration> registration; found {lists.Length}."));
            return;
        }

        try
        {
            _ = HookPointDefinitionRegistrations.ToDictionary(lists[0]);
        }
        catch (HookCompositionException exception)
        {
            diagnostics.Add(new CompositionDiagnostic("agentkit.hook-point.collision", exception.Message));
        }
    }

    private static void ValidateSingular<TService>(
        ComponentRegistrationSnapshot snapshot,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics,
        string code)
        where TService : class
    {
        var count = snapshot.Services.Count(static descriptor =>
            !descriptor.IsKeyedService && descriptor.ServiceType == typeof(TService));
        if (count != 1)
        {
            diagnostics.Add(new CompositionDiagnostic(
                $"{code}.{(count == 0 ? "missing" : "ambiguous")}",
                $"Expected exactly one unkeyed {typeof(TService).Name} registration; found {count}. Register AddAgentHooks or replace the hook kernel explicitly."));
        }
    }

    private static TService? Resolve<TService>(
        IServiceProvider provider,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics,
        string code)
        where TService : class
    {
        var service = provider.GetService<TService>();
        if (service is null)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"No {typeof(TService).Name} is registered. Call AddAgentHooks on the service collection."));
        }

        return service;
    }
}
