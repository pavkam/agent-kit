// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>Closes explicitly permitted Microsoft DI infrastructure registrations into the declared component graph.</summary>
/// <remarks>The materializer inspects only the immutable registration snapshot, implementation types, instances, and public constructor metadata. It never builds a provider, resolves a service, invokes a factory, or constructs an implementation.</remarks>
internal sealed class ComponentInfrastructureGraphMaterializer
{
    private readonly ImmutableArray<ServiceDescriptor> _services;
    private readonly ImmutableArray<ComponentRegistrationDescriptor> _explicitRegistrations;
    private readonly int _maximumDerivedRegistrations;
    private readonly HashSet<ComponentContractReference> _explicitAddresses;
    private readonly ImmutableArray<ComponentRegistrationDescriptor>.Builder _registrations;
    private readonly ImmutableArray<CompositionDiagnostic>.Builder _diagnostics =
        ImmutableArray.CreateBuilder<CompositionDiagnostic>();
    private readonly Dictionary<(int DescriptorIndex, Type ServiceType, string? Key), int> _derivedIndexes = [];
    private readonly HashSet<(int DescriptorIndex, Type ServiceType, string? Key)> _processed = [];
    private readonly ImmutableDictionary<(int OwnerIndex, int DependencyIndex), ImmutableArray<int>>.Builder
        _dependencyTargets = ImmutableDictionary.CreateBuilder<
            (int OwnerIndex, int DependencyIndex), ImmutableArray<int>>();

    private ComponentInfrastructureGraphMaterializer(ComponentRegistrationSnapshot snapshot)
    {
        Debug.Assert(snapshot is not null, "The guarded materialization boundary supplies a registration snapshot.");
        _services = snapshot.Services;
        _explicitRegistrations = snapshot.Registrations;
        _maximumDerivedRegistrations = snapshot.MaximumDerivedInfrastructureRegistrations;
        _explicitAddresses = [.. _explicitRegistrations.Select(static registration => registration.Service)];
        _registrations = _explicitRegistrations.ToBuilder();
    }

    /// <summary>Materializes permitted infrastructure nodes and deterministic diagnostics from one build-local snapshot.</summary>
    /// <param name="snapshot">The non-null immutable registrations captured for one provider build.</param>
    /// <returns>The explicit and derived closed registrations, diagnostics for unsupported or opaque evidence, and the exact per-dependency target indexes selected under Microsoft DI singular or collection rules.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    internal static (
        ImmutableArray<ComponentRegistrationDescriptor> Registrations,
        ImmutableArray<CompositionDiagnostic> Diagnostics,
        ImmutableDictionary<(int OwnerIndex, int DependencyIndex), ImmutableArray<int>> DependencyTargets)
        Materialize(ComponentRegistrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new ComponentInfrastructureGraphMaterializer(snapshot).Materialize();
    }

    private (
        ImmutableArray<ComponentRegistrationDescriptor> Registrations,
        ImmutableArray<CompositionDiagnostic> Diagnostics,
        ImmutableDictionary<(int OwnerIndex, int DependencyIndex), ImmutableArray<int>> DependencyTargets)
        Materialize()
    {
        Debug.Assert(_registrations.Count == _explicitRegistrations.Length,
            "Materialization starts with exactly the explicit component registrations.");
        for (var registrationIndex = 0; registrationIndex < _registrations.Count; registrationIndex++)
        {
            var dependencies = _registrations[registrationIndex].Dependencies;
            for (var dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                _dependencyTargets.Add(
                    (registrationIndex, dependencyIndex),
                    MaterializeDependency(registrationIndex, dependencies[dependencyIndex]));
            }
        }

        return (
            _registrations.ToImmutable(),
            _diagnostics.ToImmutable(),
            _dependencyTargets.ToImmutable());
    }

    private ImmutableArray<int> MaterializeDependency(
        int ownerIndex,
        ComponentDependencyDescriptor dependency)
    {
        Debug.Assert((uint) ownerIndex < (uint) _registrations.Count,
            "A dependency owner must identify a materialized registration.");
        Debug.Assert(dependency is not null, "Component registrations reject null dependencies.");
        var explicitTargets = FindExplicitTargets(dependency.Reference);
        if (dependency.ValidationBoundary == ComponentDependencyValidationBoundary.DeclaredComponentsOnly
            || (dependency.Cardinality != ComponentDependencyCardinality.AdditiveCollection
                && explicitTargets.Length > 0))
        {
            return explicitTargets;
        }

        var matches = FindEffectiveRegistrations(
            dependency.Reference,
            dependency.Cardinality == ComponentDependencyCardinality.AdditiveCollection);
        var targets = ImmutableArray.CreateBuilder<int>();
        var usedExplicitTargets = new HashSet<int>();
        foreach (var descriptorIndex in matches)
        {
            if (_services[descriptorIndex].ServiceType == dependency.Reference.ContractType
                && explicitTargets.Length > 0)
            {
                var service = _services[descriptorIndex];
                var explicitTarget = explicitTargets
                    .Where(index => !usedExplicitTargets.Contains(index)
                        && _explicitRegistrations[index].Lifetime == service.Lifetime
                        && ComponentRegistrationCorrespondenceValidator.MatchesImplementation(
                            service,
                            _explicitRegistrations[index].ImplementationType))
                    .Select(static index => (int?) index)
                    .FirstOrDefault();
                if (explicitTarget is { } explicitTargetIndex)
                {
                    _ = usedExplicitTargets.Add(explicitTargetIndex);
                    targets.Add(explicitTargetIndex);
                }

                continue;
            }

            if (MaterializeRegistration(descriptorIndex, dependency.Reference, ownerIndex) is { } target)
            {
                targets.Add(target);
            }
        }

        return targets.ToImmutable();
    }

    private ImmutableArray<int> FindExplicitTargets(ComponentContractReference reference)
    {
        Debug.Assert(reference is not null, "Component dependencies require a closed contract reference.");
        return [.. Enumerable.Range(0, _explicitRegistrations.Length)
            .Where(index => _explicitRegistrations[index].Service.Equals(reference))];
    }

    private ImmutableArray<int> FindEffectiveRegistrations(
        ComponentContractReference reference,
        bool additiveCollection)
    {
        Debug.Assert(reference is not null, "Component dependencies require a closed contract reference.");
        var exact = ImmutableArray.CreateBuilder<int>();
        var open = ImmutableArray.CreateBuilder<int>();
        for (var index = 0; index < _services.Length; index++)
        {
            var service = _services[index];
            if (!MatchesKey(service, reference.Key))
            {
                continue;
            }

            if (service.ServiceType == reference.ContractType)
            {
                exact.Add(index);
            }
            else if (reference.ContractType.IsConstructedGenericType
                && service.ServiceType.IsGenericTypeDefinition
                && service.ServiceType == reference.ContractType.GetGenericTypeDefinition())
            {
                open.Add(index);
            }
        }

        return additiveCollection
            ? [.. exact.Concat(open).Order()]
            : exact.Count > 0 ? exact.ToImmutable() : open.ToImmutable();
    }

    private static bool MatchesKey(ServiceDescriptor service, string? key)
    {
        Debug.Assert(service is not null, "The frozen service snapshot contains no null descriptors.");
        return key is null
            ? !service.IsKeyedService
            : service.IsKeyedService
            && service.ServiceKey is string serviceKey
            && string.Equals(serviceKey, key, StringComparison.Ordinal);
    }

    private int? MaterializeRegistration(
        int descriptorIndex,
        ComponentContractReference requestedService,
        int ownerIndex)
    {
        Debug.Assert((uint) descriptorIndex < (uint) _services.Length,
            "An infrastructure descriptor index must identify a frozen service registration.");
        Debug.Assert(requestedService is not null, "Infrastructure closure requires a closed requested service.");
        Debug.Assert((uint) ownerIndex < (uint) _registrations.Count,
            "A derived registration owner must identify a materialized registration.");
        var identity = (descriptorIndex, requestedService.ContractType, requestedService.Key);
        if (_derivedIndexes.TryGetValue(identity, out var existingIndex))
        {
            return existingIndex;
        }

        if (!_processed.Add(identity))
        {
            return null;
        }

        var descriptor = _services[descriptorIndex];
        if (_registrations.Count - _explicitRegistrations.Length >= _maximumDerivedRegistrations)
        {
            AddDiagnostic(
                "agentkit.component-infrastructure.validation-bound-exceeded",
                "Microsoft DI infrastructure graph validation exceeded its configured derived-registration bound.");
            return null;
        }

        if (IsServiceLocator(requestedService.ContractType))
        {
            AddDiagnostic(
                "agentkit.component-infrastructure.dependency-unsupported",
                $"Infrastructure service {Describe(requestedService)} is a deferred service-locator shape that requires an explicit ordinary component declaration.");
            return null;
        }

        if (HasFactory(descriptor))
        {
            AddDiagnostic(
                "agentkit.component-infrastructure.opaque-factory",
                $"Infrastructure service {Describe(requestedService)} is registered through an opaque factory and requires an explicit closed component declaration.");
            return null;
        }

        var instance = GetImplementationInstance(descriptor);
        var implementationType = instance?.GetType()
            ?? TryCloseImplementationType(descriptor, requestedService);
        if (implementationType is null)
        {
            return null;
        }

        var dependencies = instance is not null
            ? ImmutableArray<ComponentDependencyDescriptor>.Empty
            : CreateConstructorDependencies(implementationType, requestedService.Key);
        if (dependencies is null)
        {
            return null;
        }

        try
        {
            var registration = new ComponentRegistrationDescriptor(
                requestedService,
                implementationType,
                descriptor.Lifetime,
                dependencies.Value);
            var registrationIndex = _registrations.Count;
            _registrations.Add(registration);
            _derivedIndexes.Add(identity, registrationIndex);
            return registrationIndex;
        }
        catch (ArgumentException)
        {
            AddDiagnostic(
                "agentkit.component-infrastructure.registration-invalid",
                $"Infrastructure service {Describe(requestedService)} does not provide a valid closed implementation registration.");
            return null;
        }
    }

    private static bool HasFactory(ServiceDescriptor descriptor)
    {
        Debug.Assert(descriptor is not null, "The frozen service snapshot contains no null descriptors.");
        return descriptor.IsKeyedService
            ? descriptor.KeyedImplementationFactory is not null
            : descriptor.ImplementationFactory is not null;
    }

    private static object? GetImplementationInstance(ServiceDescriptor descriptor)
    {
        Debug.Assert(descriptor is not null, "The frozen service snapshot contains no null descriptors.");
        return descriptor.IsKeyedService
            ? descriptor.KeyedImplementationInstance
            : descriptor.ImplementationInstance;
    }

    private Type? TryCloseImplementationType(
        ServiceDescriptor descriptor,
        ComponentContractReference requestedService)
    {
        Debug.Assert(descriptor is not null, "The frozen service snapshot contains no null descriptors.");
        Debug.Assert(requestedService is not null, "Infrastructure closure requires a closed requested service.");
        var implementationType = descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType
            : descriptor.ImplementationType;
        if (implementationType is null)
        {
            AddDiagnostic(
                "agentkit.component-infrastructure.implementation-missing",
                $"Infrastructure service {Describe(requestedService)} has no inspectable implementation evidence.");
            return null;
        }

        if (!implementationType.ContainsGenericParameters)
        {
            return implementationType;
        }

        if (!implementationType.IsGenericTypeDefinition
            || !requestedService.ContractType.IsConstructedGenericType)
        {
            AddInvalidGenericDiagnostic(requestedService);
            return null;
        }

        try
        {
            var closed = implementationType.MakeGenericType(requestedService.ContractType.GetGenericArguments());
            if (!requestedService.ContractType.IsAssignableFrom(closed))
            {
                AddInvalidGenericDiagnostic(requestedService);
                return null;
            }

            return closed;
        }
        catch (ArgumentException)
        {
            AddInvalidGenericDiagnostic(requestedService);
            return null;
        }
        catch (TypeLoadException)
        {
            AddInvalidGenericDiagnostic(requestedService);
            return null;
        }
    }

    private void AddInvalidGenericDiagnostic(ComponentContractReference requestedService)
    {
        Debug.Assert(requestedService is not null, "Infrastructure closure requires a closed requested service.");
        AddDiagnostic(
            "agentkit.component-infrastructure.generic-closure-invalid",
            $"Infrastructure service {Describe(requestedService)} cannot close its registered implementation type for the requested contract.");
    }

    private ImmutableArray<ComponentDependencyDescriptor>? CreateConstructorDependencies(
        Type implementationType,
        string? requestedServiceKey)
    {
        Debug.Assert(implementationType is not null, "Only an inspectable implementation reaches constructor analysis.");
        ConstructorInfo[] constructors;
        try
        {
            constructors = implementationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        }
        catch (Exception)
        {
            AddConstructorDiagnostic(
                "agentkit.component-infrastructure.constructor-unresolvable",
                implementationType,
                "has no inspectable public constructor graph");
            return null;
        }

        if (constructors.SelectMany(static constructor => constructor.GetParameters())
            .Any(parameter => HasUnsafeKeyComparisonCandidate(parameter, requestedServiceKey)))
        {
            AddConstructorDiagnostic(
                "agentkit.component-infrastructure.dependency-unsupported",
                implementationType,
                "uses keyed constructor metadata whose availability cannot be proved without application key callbacks");
            return null;
        }

        var resolvable = constructors
            .Where(constructor => CanSupplyConstructor(constructor, requestedServiceKey))
            .OrderByDescending(static constructor => constructor.GetParameters().Length)
            .ThenBy(static constructor => constructor.MetadataToken)
            .ToArray();
        if (resolvable.Length == 0)
        {
            AddConstructorDiagnostic(
                "agentkit.component-infrastructure.constructor-unresolvable",
                implementationType,
                "has no public constructor whose dependencies are available");
            return null;
        }

        var selected = resolvable[0];
        var selectedParameterTypes = selected.GetParameters()
            .Select(static parameter => parameter.ParameterType)
            .ToHashSet();
        if (resolvable.Skip(1).Any(constructor => constructor.GetParameters()
                .Any(parameter => !selectedParameterTypes.Contains(parameter.ParameterType))))
        {
            AddConstructorDiagnostic(
                "agentkit.component-infrastructure.constructor-ambiguous",
                implementationType,
                "has multiple resolvable public constructors with incompatible dependency sets");
            return null;
        }

        var dependencies = ImmutableArray.CreateBuilder<ComponentDependencyDescriptor>();
        foreach (var parameter in selected.GetParameters())
        {
            if (HasUnsupportedKeyAttribute(parameter)
                || (IsServiceLocator(parameter.ParameterType)
                    && !HasExplicitDeferredLocatorProof(parameter.ParameterType)))
            {
                AddConstructorDiagnostic(
                    "agentkit.component-infrastructure.dependency-unsupported",
                    implementationType,
                    "uses a keyed attribute or service-locator constructor dependency that infrastructure closure does not support");
                return null;
            }

            if (TryGetEnumerableElement(parameter.ParameterType, out var elementType))
            {
                if (!TryCreateReference(elementType, out var collectionReference))
                {
                    AddUnsupportedParameterDiagnostic(implementationType);
                    return null;
                }

                dependencies.Add(InfrastructureDependency(
                    collectionReference,
                    ComponentDependencyCardinality.AdditiveCollection));
                continue;
            }

            if (parameter.HasDefaultValue
                && !HasUnkeyedRegistration(parameter.ParameterType))
            {
                continue;
            }

            if (!TryCreateReference(parameter.ParameterType, out var reference))
            {
                AddUnsupportedParameterDiagnostic(implementationType);
                return null;
            }

            var present = HasEvidence(reference);
            if (!present && parameter.HasDefaultValue)
            {
                continue;
            }

            dependencies.Add(InfrastructureDependency(
                reference,
                parameter.HasDefaultValue
                    ? ComponentDependencyCardinality.OptionalSingular
                    : ComponentDependencyCardinality.RequiredSingular));
        }

        return dependencies.ToImmutable();
    }

    private bool HasUnkeyedRegistration(Type serviceType)
    {
        Debug.Assert(serviceType is not null, "Constructor parameter types are non-null.");
        return _services.Any(service => !service.IsKeyedService
            && (service.ServiceType == serviceType
                || (serviceType.IsConstructedGenericType
                    && service.ServiceType.IsGenericTypeDefinition
                    && service.ServiceType == serviceType.GetGenericTypeDefinition())));
    }

    private bool CanSupplyConstructor(ConstructorInfo constructor, string? requestedServiceKey)
    {
        Debug.Assert(constructor is not null, "Reflection returns non-null constructor metadata.");
        return constructor.GetParameters().All(parameter => CanSupplyParameter(parameter, requestedServiceKey));
    }

    private bool CanSupplyParameter(ParameterInfo parameter, string? requestedServiceKey)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        return HasServiceKeyAttribute(parameter)
            ? requestedServiceKey is not null || parameter.HasDefaultValue
            : HasFromKeyedServicesAttribute(parameter)
            ? CanSupplyFromKeyedServices(parameter, requestedServiceKey) || parameter.HasDefaultValue
            : TryGetEnumerableElement(parameter.ParameterType, out _)
            || parameter.HasDefaultValue
            || IsServiceLocator(parameter.ParameterType)
            || (TryCreateReference(parameter.ParameterType, out var reference)
                && HasEvidence(reference));
    }

    private bool HasEvidence(ComponentContractReference reference)
    {
        Debug.Assert(reference is not null, "Constructor dependencies require a closed reference.");
        return _explicitAddresses.Contains(reference)
            || FindEffectiveRegistrations(reference, additiveCollection: false).Length > 0;
    }

    private bool CanSupplyFromKeyedServices(ParameterInfo parameter, string? requestedServiceKey)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        var attribute = FindFromKeyedServicesAttribute(parameter);
        Debug.Assert(attribute is not null, "The caller found FromKeyedServices metadata.");
        if (attribute.ConstructorArguments.Count == 0)
        {
            return requestedServiceKey is not null
                && (TryGetEnumerableElement(parameter.ParameterType, out _)
                    || HasKeyedRegistration(parameter.ParameterType, requestedServiceKey));
        }

        var key = attribute.ConstructorArguments[0].Value;
        return key switch
        {
            null => TryGetEnumerableElement(parameter.ParameterType, out _)
                || HasUnkeyedRegistration(parameter.ParameterType),
            string stringKey => TryGetEnumerableElement(parameter.ParameterType, out _)
                || HasKeyedRegistration(parameter.ParameterType, stringKey),
            _ => false,
        };
    }

    private bool HasKeyedRegistration(Type serviceType, string key)
    {
        Debug.Assert(serviceType is not null, "Constructor parameter types are non-null.");
        Debug.Assert(key is not null, "Safe keyed matching requires a non-null string key.");
        var candidateType = TryGetEnumerableElement(serviceType, out var elementType)
            ? elementType
            : serviceType;
        return _services.Any(service => service.IsKeyedService
            && service.ServiceKey is string serviceKey
            && string.Equals(serviceKey, key, StringComparison.Ordinal)
            && (service.ServiceType == candidateType
                || (candidateType.IsConstructedGenericType
                    && service.ServiceType.IsGenericTypeDefinition
                    && service.ServiceType == candidateType.GetGenericTypeDefinition())));
    }

    private static bool TryGetEnumerableElement(Type type, [NotNullWhen(true)] out Type? elementType)
    {
        Debug.Assert(type is not null, "Constructor parameter types are non-null.");
        if (type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static bool TryCreateReference(
        Type contractType,
        [NotNullWhen(true)] out ComponentContractReference? reference)
    {
        Debug.Assert(contractType is not null, "Constructor parameter types are non-null.");
        try
        {
            reference = new ComponentContractReference(contractType);
            return true;
        }
        catch (ArgumentException)
        {
            reference = null;
            return false;
        }
    }

    private static bool HasUnsupportedKeyAttribute(ParameterInfo parameter)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        return parameter.CustomAttributes.Any(static attribute =>
            attribute.AttributeType.FullName is
                "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute"
                or "Microsoft.Extensions.DependencyInjection.ServiceKeyAttribute");
    }

    private static bool HasServiceKeyAttribute(ParameterInfo parameter)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        return parameter.CustomAttributes.Any(static attribute =>
            attribute.AttributeType.FullName
                == "Microsoft.Extensions.DependencyInjection.ServiceKeyAttribute");
    }

    private bool HasUnsafeKeyComparisonCandidate(
        ParameterInfo parameter,
        string? requestedServiceKey)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        var attribute = FindFromKeyedServicesAttribute(parameter);
        if (attribute is null)
        {
            return false;
        }

        if (attribute.ConstructorArguments.Count == 0)
        {
            return requestedServiceKey is null
                || HasNonStringKeyCandidate(parameter.ParameterType);
        }

        var key = attribute.ConstructorArguments[0].Value;
        return key is not null and not string
            || (key is string && HasNonStringKeyCandidate(parameter.ParameterType));
    }

    private bool HasNonStringKeyCandidate(Type serviceType)
    {
        Debug.Assert(serviceType is not null, "Constructor parameter types are non-null.");
        var candidateType = TryGetEnumerableElement(serviceType, out var elementType)
            ? elementType
            : serviceType;
        return _services.Any(service => service.IsKeyedService
            && service.ServiceKey is not null and not string
            && (service.ServiceType == candidateType
                || (candidateType.IsConstructedGenericType
                    && service.ServiceType.IsGenericTypeDefinition
                    && service.ServiceType == candidateType.GetGenericTypeDefinition())));
    }

    private static bool HasFromKeyedServicesAttribute(ParameterInfo parameter)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        return FindFromKeyedServicesAttribute(parameter) is not null;
    }

    private static CustomAttributeData? FindFromKeyedServicesAttribute(ParameterInfo parameter)
    {
        Debug.Assert(parameter is not null, "Reflection returns non-null parameter metadata.");
        return parameter.CustomAttributes.FirstOrDefault(static candidate =>
            candidate.AttributeType.FullName
                == "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute");
    }

    private bool HasExplicitDeferredLocatorProof(Type type)
    {
        Debug.Assert(type is not null, "Constructor parameter types are non-null.");
        return IsDeferredLocator(type)
            && TryCreateReference(type, out var reference)
            && _explicitAddresses.Contains(reference);
    }

    private static bool IsServiceLocator(Type type)
    {
        Debug.Assert(type is not null, "Constructor parameter types are non-null.");
        return type == typeof(IServiceProvider)
            || type == typeof(IServiceScopeFactory)
            || IsDeferredLocator(type)
            || type.FullName is
                "Microsoft.Extensions.DependencyInjection.IKeyedServiceProvider"
                or "Microsoft.Extensions.DependencyInjection.IServiceProviderIsService"
                or "Microsoft.Extensions.DependencyInjection.IServiceProviderIsKeyedService";
    }

    private static bool IsDeferredLocator(Type type)
    {
        Debug.Assert(type is not null, "Constructor parameter types are non-null.");
        return type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() is var genericDefinition
            && (genericDefinition == typeof(Func<>) || genericDefinition == typeof(Lazy<>));
    }

    private static ComponentDependencyDescriptor InfrastructureDependency(
        ComponentContractReference reference,
        ComponentDependencyCardinality cardinality)
    {
        Debug.Assert(reference is not null, "Derived infrastructure dependencies require a closed reference.");
        return new ComponentDependencyDescriptor(
            reference,
            cardinality,
            factoryBoundary: null,
            ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure);
    }

    private void AddUnsupportedParameterDiagnostic(Type implementationType)
    {
        Debug.Assert(implementationType is not null, "Only an inspectable implementation reaches constructor analysis.");
        AddConstructorDiagnostic(
            "agentkit.component-infrastructure.dependency-unsupported",
            implementationType,
            "uses a constructor parameter that cannot be represented as a closed reference dependency");
    }

    private void AddConstructorDiagnostic(string code, Type implementationType, string reason)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(code), "Infrastructure diagnostics require a stable code.");
        Debug.Assert(implementationType is not null, "Infrastructure diagnostics require an implementation type.");
        Debug.Assert(!string.IsNullOrWhiteSpace(reason), "Infrastructure diagnostics require a safe reason.");
        AddDiagnostic(
            code,
            $"Infrastructure implementation {implementationType.FullName ?? implementationType.Name} {reason}.");
    }

    private void AddDiagnostic(string code, string safeMessage)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(code), "Infrastructure diagnostics require a stable code.");
        Debug.Assert(!string.IsNullOrWhiteSpace(safeMessage), "Infrastructure diagnostics require a safe message.");
        _diagnostics.Add(new CompositionDiagnostic(code, safeMessage));
    }

    private static string Describe(ComponentContractReference reference)
    {
        Debug.Assert(reference is not null, "Infrastructure diagnostics require a closed contract reference.");
        return reference.ContractType.FullName ?? reference.ContractType.Name;
    }
}
