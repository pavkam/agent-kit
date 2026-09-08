// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

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
        UnrepresentedRequiredSpine = CreateUnrepresentedRequiredSpine(registrations);
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

    /// <summary>Gets current reduced-spine service contracts that have no explicit declaration.</summary>
    /// <value>Immutable unkeyed contract references used only as honest partial-readiness evidence.</value>
    internal ImmutableArray<ComponentContractReference> UnrepresentedRequiredSpine { get; }

    /// <summary>Gets whether this step-one metadata snapshot attests to the complete normative runnable graph.</summary>
    /// <value>Always <see langword="false"/> until every real spine owner publishes descriptors in the staged rollout.</value>
    internal bool RepresentsCompleteRunnableGraph { get; }

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

    /// <summary>Finds required reduced-spine contracts that are not represented by explicit declarations.</summary>
    /// <param name="registrations">The validated non-default declarations frozen for this build.</param>
    /// <returns>Unkeyed required contract references that remain outside the declared partial graph.</returns>
    private static ImmutableArray<ComponentContractReference> CreateUnrepresentedRequiredSpine(
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
    {
        Debug.Assert(!registrations.IsDefault, "The snapshot constructor rejects a default declaration collection.");
        var declared = registrations.Select(static registration => registration.Service).ToHashSet();
        ImmutableArray<ComponentContractReference> required =
        [
            ComponentContractReference.Unkeyed<IAgentDefinitionCatalog>(),
            ComponentContractReference.Unkeyed<IAgentRunProfilePublicationReader>(),
            ComponentContractReference.Unkeyed<ISecurityProfileSelector>(),
            ComponentContractReference.Unkeyed<ISecurityGrantStore>(),
            ComponentContractReference.Unkeyed<TimeProvider>(),
            ComponentContractReference.Unkeyed<IIdentifierGenerator<RunId>>(),
            ComponentContractReference.Unkeyed<IIdentifierGenerator<OperationId>>(),
            ComponentContractReference.Unkeyed<IAgentLoop>(),
        ];
        return [.. required.Where(reference => !declared.Contains(reference))];
    }
}
