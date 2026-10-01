// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Implements the registration behavior behind <see cref="ServiceExtensions"/>.</summary>
/// <remarks>Every method is idempotent for an identical registration, rejects a conflicting duplicate, and never builds a service provider. No concrete store, embedding model, or reranker is ever registered by this type.</remarks>
internal static class MemoryServiceRegistration
{
    internal static IServiceCollection AddAgentMemory(IServiceCollection services, Action<AgentMemoryOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentKitObservability();
        var optionsBuilder = services.AddOptions<AgentMemoryOptions>()
            .Validate(static options => Enum.IsDefined(options.AcceptanceMode), "AcceptanceMode must be a defined value.")
            .Validate(static options => options.MaximumRetrievedItems > 0, "MaximumRetrievedItems must be positive.")
            .Validate(static options => options.MaximumRetrievedBytes > 0, "MaximumRetrievedBytes must be positive.")
            .Validate(static options => options.MaximumRetrievalTokens > 0, "MaximumRetrievalTokens must be positive.")
            .Validate(static options => options.GrantLifetime > TimeSpan.Zero, "GrantLifetime must be positive.")
            .Validate(static options => options.EstimatedBytesPerToken > 0 && double.IsFinite(options.EstimatedBytesPerToken), "EstimatedBytesPerToken must be positive and finite.")
            .Validate(static options => options.MaximumSemanticRequestsPerOperation > 0, "MaximumSemanticRequestsPerOperation must be positive.")
            .ValidateOnStart();
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<MemoryId>, GuidMemoryIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<DocumentId>, GuidDocumentIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<RetrievalRequestId>, GuidRetrievalRequestIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
        services.TryAddSingleton<IDocumentChunker, DeterministicTextChunker>();
        services.TryAddSingleton(static provider =>
        {
            var registry = new MemoryProfileRegistry();
            foreach (var contributor in provider.GetServices<IMemoryProfileContributor>())
            {
                contributor.Contribute(registry);
            }

            return registry;
        });
        services.TryAddSingleton<IMemoryProfileCatalog, MemoryProfileCatalog>();
        services.TryAddSingleton<IMemoryStoreCatalog, DefaultMemoryStoreCatalog>();
        services.TryAddSingleton<IMemoryStoreSelector, DefaultMemoryStoreSelector>();
        services.TryAddSingleton<IRetrievalBudgetPolicy, BoundedRetrievalBudgetPolicy>();
        services.TryAddSingleton<IMemoryEventDispatcher, DefaultMemoryEventDispatcher>();
        services.TryAddSingleton<IMemoryPolicyDispatcher, DefaultMemoryPolicyDispatcher>();
        services.TryAddSingleton<IMemoryProfileRuntimeSelector, DefaultMemoryProfileRuntimeSelector>();
        services.TryAddSingleton<MemoryGrantIssuer>();
        services.TryAddSingleton<IMemoryCoordinator, DefaultMemoryCoordinator>();
        services.TryAddSingleton<IDocumentLifecycleCoordinator, DefaultDocumentLifecycleCoordinator>();
        services.TryAddSingleton<IRetrievalPipeline, RetrievalPipeline>();
        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(MemoryPolicyDeclaration)
                && descriptor.ImplementationInstance is MemoryPolicyDeclaration { Registration.Id: var id }
                && id == FailClosedMemoryPolicy.Id))
        {
            services.TryAddSingleton<FailClosedMemoryPolicy>();
            _ = services.AddSingleton(new MemoryPolicyDeclaration(
                new MemoryPolicyRegistration(MemoryPolicyProfileKeys.FailClosed, FailClosedMemoryPolicy.Id, int.MaxValue, ServiceLifetime.Singleton), typeof(FailClosedMemoryPolicy)));
        }

        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(QueryRewriterDeclaration)
                && descriptor.ImplementationInstance is QueryRewriterDeclaration { Descriptor.Key: var key }
                && key == NoRewriteQueryRewriter.Key))
        {
            _ = services.AddSingleton(new QueryRewriterDeclaration(NoRewriteQueryRewriter.DescriptorValue, typeof(NoRewriteQueryRewriter)));
            _ = services.AddKeyedSingleton<IQueryRewriter, NoRewriteQueryRewriter>(NoRewriteQueryRewriter.Key.Value);
        }

        return services;
    }

    internal static IServiceCollection AddMemoryProfile(IServiceCollection services, MemoryProfileKey key, Action<MemoryProfileOptions> configure, bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddAgentMemory(services, configure: null);
        return services.AddSingleton<IMemoryProfileContributor>(new MemoryProfileContributor(key, configure, replace));
    }

    internal static IServiceCollection AddKeyed<TService, TImplementation>(IServiceCollection services, string? key, string parameterName, bool replace)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key, parameterName);
        _ = AddAgentMemory(services, configure: null);
        var existing = services.Where(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(TService) && Equals(descriptor.ServiceKey, key)).ToList();
        if (replace)
        {
            foreach (var descriptor in existing)
            {
                _ = services.Remove(descriptor);
            }
        }
        else if (existing.Count > 0)
        {
            return existing.All(descriptor => descriptor.KeyedImplementationType == typeof(TImplementation))
                ? services
                : throw new InvalidOperationException($"A {typeof(TService).Name} with key '{key}' is already registered; use the matching Replace method to change it.");
        }

        return services.AddKeyedSingleton<TService, TImplementation>(key);
    }

    internal static IServiceCollection AddPolicy<TPolicy>(IServiceCollection services, MemoryPolicyRegistration registration, bool replace)
        where TPolicy : class, IMemoryPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);
        _ = AddAgentMemory(services, configure: null);
        var existing = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(MemoryPolicyDeclaration)
            && descriptor.ImplementationInstance is MemoryPolicyDeclaration declaration
            && declaration.Registration.Id == registration.Id);
        if (existing is not null)
        {
            var previous = (MemoryPolicyDeclaration) existing.ImplementationInstance!;
            if (!replace)
            {
                return previous.PolicyType == typeof(TPolicy) && previous.Registration == registration
                    ? services
                    : throw new InvalidOperationException($"A memory policy with identity '{registration.Id.Value}' is already registered; use ReplaceMemoryPolicy to change it.");
            }

            _ = services.Remove(existing);
        }

        services.Add(new ServiceDescriptor(typeof(TPolicy), typeof(TPolicy), registration.Lifetime));
        return services.AddSingleton(new MemoryPolicyDeclaration(registration, typeof(TPolicy)));
    }

    internal static IServiceCollection AddEventSink<TSink>(IServiceCollection services, MemoryEventSinkRegistration registration)
        where TSink : class, IMemoryEventSink
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);
        _ = AddAgentMemory(services, configure: null);
        var existing = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(MemoryEventSinkDeclaration)
            && descriptor.ImplementationInstance is MemoryEventSinkDeclaration declaration
            && declaration.Registration.Id == registration.Id);
        if (existing?.ImplementationInstance is MemoryEventSinkDeclaration previous)
        {
            return previous.SinkType == typeof(TSink) && previous.Registration == registration
                ? services
                : throw new InvalidOperationException($"A memory event sink with identity '{registration.Id.Value}' is already registered differently.");
        }

        services.Add(new ServiceDescriptor(typeof(TSink), typeof(TSink), registration.Lifetime));
        return services.AddSingleton(new MemoryEventSinkDeclaration(registration, typeof(TSink)));
    }

    internal static IServiceCollection AddQueryRewriter<TRewriter>(IServiceCollection services, QueryRewriterDescriptor descriptor, bool replace)
        where TRewriter : class, IQueryRewriter
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        _ = AddKeyed<IQueryRewriter, TRewriter>(services, descriptor.Key.Value, nameof(descriptor), replace);
        var existing = services.Where(candidate => candidate.ServiceType == typeof(QueryRewriterDeclaration)
            && candidate.ImplementationInstance is QueryRewriterDeclaration declaration && declaration.Descriptor.Key == descriptor.Key).ToList();
        foreach (var candidate in existing)
        {
            _ = services.Remove(candidate);
        }

        return services.AddSingleton(new QueryRewriterDeclaration(descriptor, typeof(TRewriter)));
    }

    internal static IServiceCollection ReplaceSingular<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentMemory(services, configure: null);
        return services.Replace(ServiceDescriptor.Singleton<TService, TImplementation>());
    }

    internal static IServiceCollection AddDurableMemorySource(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentMemory(services, configure: null);
        services.TryAddKeyedSingleton<IRetrievalSource, DurableMemoryRetrievalSource>(DurableMemoryRetrievalSource.Key.Value);
        return services;
    }

    internal static IServiceCollection AddDocumentSource(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentMemory(services, configure: null);
        services.TryAddKeyedSingleton<IRetrievalSource, DocumentChunkRetrievalSource>(DocumentChunkRetrievalSource.Key.Value);
        return services;
    }
}
