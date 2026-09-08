// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates that explicit component declarations correspond to the frozen Microsoft DI registrations.</summary>
/// <remarks>The validator is pure and never resolves services or invokes factories. Direct implementation and instance registrations expose a concrete type that must match exactly. Opaque factories are matched only by their declared contract, exact key, and lifetime; their explicit component declaration remains the sole implementation and dependency evidence.</remarks>
internal static class ComponentRegistrationCorrespondenceValidator
{
    /// <summary>Validates declaration uniqueness and exact service-registration correspondence.</summary>
    /// <param name="snapshot">The non-null build-local registration snapshot.</param>
    /// <returns>Every deterministic safe diagnostic found, or an empty collection when all declarations correspond.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    internal static ImmutableArray<CompositionDiagnostic> Validate(ComponentRegistrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        ValidateMetadataRegistrations(snapshot.Services, diagnostics);
        ValidateDuplicateDeclarations(snapshot.Registrations, diagnostics);

        var matchedServices = new HashSet<int>();
        var declaredAddresses = snapshot.Registrations.Select(static registration => registration.Service).ToHashSet();
        foreach (var registration in snapshot.Registrations)
        {
            ValidateRegistration(registration, snapshot.Services, matchedServices, diagnostics);
        }

        ValidateUndeclaredServices(snapshot.Services, declaredAddresses, matchedServices, diagnostics);
        return diagnostics.ToImmutable();
    }

    /// <summary>Rejects component metadata registered through an opaque DI shape.</summary>
    /// <param name="services">The captured non-default Microsoft DI descriptors.</param>
    /// <param name="diagnostics">The initialized result collector.</param>
    private static void ValidateMetadataRegistrations(
        ImmutableArray<ServiceDescriptor> services,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!services.IsDefault, "A registration snapshot always contains an initialized service collection.");
        Debug.Assert(diagnostics is not null, "Validation owns an initialized diagnostic collector.");
        foreach (var service in services)
        {
            if (service.ServiceType == typeof(ComponentRegistrationDescriptor)
                && (service.IsKeyedService
                    || service.ImplementationInstance is not ComponentRegistrationDescriptor))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.component-registration.metadata-opaque",
                    "Component registration metadata must be supplied as an immutable instance declaration."));
            }
        }
    }

    /// <summary>Rejects repeated declarations that claim the same implementation registration.</summary>
    /// <param name="registrations">The captured non-default explicit declarations.</param>
    /// <param name="diagnostics">The initialized result collector.</param>
    private static void ValidateDuplicateDeclarations(
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!registrations.IsDefault, "A registration snapshot always contains initialized declarations.");
        Debug.Assert(diagnostics is not null, "Validation owns an initialized diagnostic collector.");
        foreach (var group in registrations.GroupBy(static registration =>
                     (registration.Service, registration.ImplementationType, registration.Lifetime)))
        {
            if (group.Count() > 1)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.component-registration.descriptor-duplicate",
                    $"Component {Describe(group.Key.Service)} declares {group.Key.ImplementationType.FullName} with {group.Key.Lifetime} lifetime more than once."));
            }
        }
    }

    /// <summary>Matches one explicit declaration to exactly one actual service registration.</summary>
    /// <param name="registration">The non-null explicit component declaration.</param>
    /// <param name="services">The captured non-default Microsoft DI descriptors.</param>
    /// <param name="matchedServices">Indexes already matched to declarations.</param>
    /// <param name="diagnostics">The initialized result collector.</param>
    private static void ValidateRegistration(
        ComponentRegistrationDescriptor registration,
        ImmutableArray<ServiceDescriptor> services,
        HashSet<int> matchedServices,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(registration is not null, "Snapshot construction rejects null declarations.");
        Debug.Assert(!services.IsDefault, "A registration snapshot always contains initialized service descriptors.");
        Debug.Assert(matchedServices is not null, "Correspondence validation owns an initialized match set.");
        Debug.Assert(diagnostics is not null, "Validation owns an initialized diagnostic collector.");

        var addressMatches = Enumerable.Range(0, services.Length)
            .Where(index => MatchesAddress(services[index], registration.Service))
            .ToArray();
        if (addressMatches.Length == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-registration.service-missing",
                $"Declared component {Describe(registration.Service)} has no corresponding Microsoft DI registration."));
            return;
        }

        var lifetimeMatches = addressMatches
            .Where(index => services[index].Lifetime == registration.Lifetime)
            .ToArray();
        if (lifetimeMatches.Length == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-registration.lifetime-mismatch",
                $"Declared component {Describe(registration.Service)} uses {registration.Lifetime} lifetime, but its Microsoft DI registration does not."));
            return;
        }

        var implementationMatches = lifetimeMatches
            .Where(index => MatchesImplementation(services[index], registration.ImplementationType))
            .ToArray();
        if (implementationMatches.Length == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-registration.implementation-mismatch",
                $"Declared component {Describe(registration.Service)} names {registration.ImplementationType.FullName}, but the observable Microsoft DI implementation type differs."));
            return;
        }

        var unmatched = implementationMatches.Where(index => !matchedServices.Contains(index)).ToArray();
        if (unmatched.Length != 1 || implementationMatches.Length != 1)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-registration.service-duplicate",
                $"Declared component {Describe(registration.Service)} does not correspond to exactly one Microsoft DI registration."));
            return;
        }

        _ = matchedServices.Add(unmatched[0]);
    }

    /// <summary>Rejects extra actual registrations at any address claimed by explicit metadata.</summary>
    /// <param name="services">The captured non-default Microsoft DI descriptors.</param>
    /// <param name="declaredAddresses">Exact addresses present in explicit metadata.</param>
    /// <param name="matchedServices">Indexes successfully paired to declarations.</param>
    /// <param name="diagnostics">The initialized result collector.</param>
    private static void ValidateUndeclaredServices(
        ImmutableArray<ServiceDescriptor> services,
        HashSet<ComponentContractReference> declaredAddresses,
        HashSet<int> matchedServices,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!services.IsDefault, "A registration snapshot always contains initialized service descriptors.");
        Debug.Assert(declaredAddresses is not null, "Correspondence validation owns initialized declared addresses.");
        Debug.Assert(matchedServices is not null, "Correspondence validation owns an initialized match set.");
        Debug.Assert(diagnostics is not null, "Validation owns an initialized diagnostic collector.");
        for (var index = 0; index < services.Length; index++)
        {
            var address = declaredAddresses.FirstOrDefault(reference => MatchesAddress(services[index], reference));
            if (address is not null && !matchedServices.Contains(index))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.component-registration.service-undeclared",
                    $"Microsoft DI contains an undeclared registration at claimed address {Describe(address)}."));
            }
        }
    }

    /// <summary>Determines whether a DI descriptor uses an exact declared contract and ordinal string key.</summary>
    /// <param name="service">The non-null captured DI descriptor.</param>
    /// <param name="reference">The non-null declared service address.</param>
    /// <returns><see langword="true"/> only for an exact unkeyed or ordinal string-keyed address match.</returns>
    private static bool MatchesAddress(ServiceDescriptor service, ComponentContractReference reference)
    {
        Debug.Assert(service is not null, "Microsoft DI service collections reject null descriptors.");
        Debug.Assert(reference is not null, "Component declarations reject null service references.");
        return service.ServiceType == reference.ContractType
            && (reference.Key is null
                ? !service.IsKeyedService
                : service.IsKeyedService
                && service.ServiceKey is string key
                && string.Equals(key, reference.Key, StringComparison.Ordinal));
    }

    /// <summary>Checks an observable implementation type or accepts an explicitly declared opaque factory.</summary>
    /// <param name="service">The non-null captured DI descriptor.</param>
    /// <param name="implementationType">The declared concrete implementation type.</param>
    /// <returns><see langword="true"/> when observable type evidence matches exactly or the DI registration is an opaque factory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> or <paramref name="implementationType"/> is <see langword="null"/>.</exception>
    internal static bool MatchesImplementation(ServiceDescriptor service, Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(implementationType);
        var observedType = service.IsKeyedService
            ? service.KeyedImplementationType ?? service.KeyedImplementationInstance?.GetType()
            : service.ImplementationType ?? service.ImplementationInstance?.GetType();
        return observedType is null || observedType == implementationType;
    }

    /// <summary>Formats a contract address without resolving or exposing service content.</summary>
    /// <param name="reference">The non-null declared service address.</param>
    /// <returns>A stable safe contract-and-key description.</returns>
    private static string Describe(ComponentContractReference reference)
    {
        Debug.Assert(reference is not null, "Component declarations reject null service references.");
        return reference.Key is null
            ? reference.ContractType.FullName!
            : $"{reference.ContractType.FullName} keyed '{reference.Key}'";
    }
}
