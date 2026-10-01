// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using AgentKit.Internal;

/// <summary>Freezes declared component metadata and the corresponding Microsoft DI registrations for one composition build.</summary>
/// <remarks>The snapshot retains immutable references to <see cref="ServiceDescriptor"/> values without resolving them. It is validation evidence for one provider build and is never shared as a cache across separately built providers.</remarks>
internal sealed record ComponentRegistrationSnapshot
{
    /// <summary>Initializes one immutable build-local registration snapshot.</summary>
    /// <param name="services">The non-default Microsoft DI registrations captured in collection order.</param>
    /// <param name="registrations">The non-default explicit component declarations captured in declaration order.</param>
    /// <exception cref="ArgumentException"><paramref name="services"/> or <paramref name="registrations"/> is default, or either collection contains a null element.</exception>
    internal ComponentRegistrationSnapshot(
        ImmutableArray<ServiceDescriptor> services,
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
        : this(
            services,
            registrations,
            AgentKitCompositionOptions.DefaultMaximumDerivedInfrastructureRegistrations)
    {
    }

    /// <summary>Initializes one immutable build-local registration snapshot with a validated infrastructure-derivation bound.</summary>
    /// <param name="services">The non-default Microsoft DI registrations captured in collection order.</param>
    /// <param name="registrations">The non-default explicit component declarations captured in declaration order.</param>
    /// <param name="maximumDerivedInfrastructureRegistrations">The positive maximum derived infrastructure registrations allowed for this build.</param>
    /// <exception cref="ArgumentException"><paramref name="services"/> or <paramref name="registrations"/> is default, or either collection contains a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumDerivedInfrastructureRegistrations"/> is less than one.</exception>
    internal ComponentRegistrationSnapshot(
        ImmutableArray<ServiceDescriptor> services,
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        int maximumDerivedInfrastructureRegistrations)
    {
        ArgumentException.ThrowIfContainsNull(services);
        ArgumentException.ThrowIfContainsNull(registrations);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDerivedInfrastructureRegistrations, 1);

        Services = services;
        Registrations = registrations;
        MaximumDerivedInfrastructureRegistrations = maximumDerivedInfrastructureRegistrations;
        UnrepresentedRequiredSpine = CreateUnrepresentedRequiredSpine(services, registrations);
    }

    /// <summary>Gets the complete Microsoft DI registration collection frozen for this build.</summary>
    /// <value>Non-default descriptors in the exact collection order observed without executing factories.</value>
    internal ImmutableArray<ServiceDescriptor> Services { get; }

    /// <summary>Gets the additive component declarations frozen for this build.</summary>
    /// <value>Non-default declarations in registration order, including duplicates for deterministic rejection.</value>
    internal ImmutableArray<ComponentRegistrationDescriptor> Registrations { get; }

    /// <summary>Gets the provider-local maximum number of registrations opt-in infrastructure validation may derive.</summary>
    /// <value>A validated positive bound captured before graph materialization or application service activation.</value>
    internal int MaximumDerivedInfrastructureRegistrations { get; }

    /// <summary>Gets required spine and selected service addresses that have no explicit declaration.</summary>
    /// <value>
    /// Every engine-wide singular contract the facade requires, plus every keyed registration of a definition-selectable
    /// contract actually present in the frozen registrations, whose address no component declaration covers.
    /// </value>
    internal ImmutableArray<ComponentContractReference> UnrepresentedRequiredSpine { get; }

    /// <summary>Gets whether the explicit declarations cover the complete runnable graph this snapshot froze.</summary>
    /// <value>
    /// <see langword="true"/> only when <see cref="UnrepresentedRequiredSpine"/> is empty: every engine-wide singular and
    /// every registered selectable keyed component has an explicit declaration. A snapshot built from registrations
    /// that publish no descriptors is therefore honestly <see langword="false"/>.
    /// </value>
    internal bool RepresentsCompleteRunnableGraph => UnrepresentedRequiredSpine.IsEmpty;

    /// <summary>Captures one immutable snapshot without resolving services or invoking registration factories.</summary>
    /// <param name="services">The mutable service collection to copy at the boundary of one build.</param>
    /// <returns>A build-local immutable snapshot of actual registrations and explicit instance declarations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="services"/> contains a null descriptor.</exception>
    internal static ComponentRegistrationSnapshot Capture(IServiceCollection services)
        => Capture(
            services,
            AgentKitCompositionOptions.DefaultMaximumDerivedInfrastructureRegistrations);

    /// <summary>Captures one immutable snapshot with an explicit infrastructure-derivation bound.</summary>
    /// <param name="services">The mutable service collection to copy at the boundary of one build.</param>
    /// <param name="maximumDerivedInfrastructureRegistrations">The positive provider-local derivation bound.</param>
    /// <returns>A build-local immutable snapshot of actual registrations, instance declarations, and the validation bound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="services"/> contains a null descriptor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumDerivedInfrastructureRegistrations"/> is outside the supported range.</exception>
    internal static ComponentRegistrationSnapshot Capture(
        IServiceCollection services,
        int maximumDerivedInfrastructureRegistrations)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDerivedInfrastructureRegistrations, 1);

        var capturedServices = services.ToImmutableArray();
        ArgumentException.ThrowIfContainsNull(capturedServices, nameof(services));
        var registrations = capturedServices
            .Where(static descriptor => !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(ComponentRegistrationDescriptor))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<ComponentRegistrationDescriptor>()
            .ToImmutableArray();
        return new ComponentRegistrationSnapshot(
            capturedServices,
            registrations,
            maximumDerivedInfrastructureRegistrations);
    }

    /// <summary>Finds required engine-wide contracts and registered selectable components that have no explicit declaration.</summary>
    /// <param name="services">The validated non-default Microsoft DI registrations frozen for this build.</param>
    /// <param name="registrations">The validated non-default declarations frozen for this build.</param>
    /// <returns>Contract references, in deterministic order, that remain outside the declared graph.</returns>
    private static ImmutableArray<ComponentContractReference> CreateUnrepresentedRequiredSpine(
        ImmutableArray<ServiceDescriptor> services,
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
    {
        Debug.Assert(!services.IsDefault, "The snapshot constructor rejects default registrations.");
        Debug.Assert(!registrations.IsDefault, "The snapshot constructor rejects a default declaration collection.");
        var declared = registrations.Select(static registration => registration.Service).ToHashSet();
        ImmutableArray<ComponentContractReference> engineWide =
        [
            ComponentContractReference.Unkeyed<IAgentDefinitionCatalog>(),
            ComponentContractReference.Unkeyed<IAgentRunProfilePublicationReader>(),
            ComponentContractReference.Unkeyed<IAgentRunScopeFactory>(),
            ComponentContractReference.Unkeyed<ISecurityProfileSelector>(),
            ComponentContractReference.Unkeyed<ISecurityAuthoritySelector>(),
            ComponentContractReference.Unkeyed<ISecurityPolicyCatalog>(),
            ComponentContractReference.Unkeyed<ISecurityGrantStore>(),
            ComponentContractReference.Unkeyed<IApprovalBroker>(),
            ComponentContractReference.Unkeyed<ISessionDirectory>(),
            ComponentContractReference.Unkeyed<ISessionStoreCatalog>(),
            ComponentContractReference.Unkeyed<ISessionStoreSelector>(),
            ComponentContractReference.Unkeyed<IHookDispatcher>(),
            ComponentContractReference.Unkeyed<IHookCatalog>(),
            ComponentContractReference.Unkeyed<IHookProfileSelector>(),
            ComponentContractReference.Unkeyed<IModelCatalog>(),
            ComponentContractReference.Unkeyed<IProviderProfileRuntimeSelector>(),
            ComponentContractReference.Unkeyed<IBudgetAuthority>(),
            ComponentContractReference.Unkeyed<TimeProvider>(),
            ComponentContractReference.Unkeyed<IRandomizerFactory>(),
            ComponentContractReference.Unkeyed<IContentHasher>(),
            ComponentContractReference.Unkeyed<IIdentifierGenerator<RunId>>(),
            ComponentContractReference.Unkeyed<IIdentifierGenerator<OperationId>>(),
        ];
        var selectable = new HashSet<Type>
        {
            typeof(IAgentLoop),
            typeof(IRunContinuationPolicy),
            typeof(IInputCoordinator),
            typeof(IOutputPublisher),
            typeof(IOutputProcessor),
            typeof(IContextAssembler),
            typeof(IModelSelector),
            typeof(IModelRequestExecutor),
            typeof(IToolExecutor),
        };
        var registeredSelectable = services
            .Where(service => service.IsKeyedService
                && service.ServiceKey is string
                && selectable.Contains(service.ServiceType))
            .Select(static service => new ComponentContractReference(service.ServiceType, (string) service.ServiceKey!))
            .Distinct();
        return [.. engineWide.Concat(registeredSelectable).Where(reference => !declared.Contains(reference))];
    }
}
