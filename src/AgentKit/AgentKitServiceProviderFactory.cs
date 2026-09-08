// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds Microsoft DI providers with exact AgentKit composition evidence captured at the provider boundary.</summary>
/// <remarks>
/// <para>
/// Hosted applications configure this factory through the standard .NET host
/// service-provider-factory surface. The factory copies the completed service
/// collection, removes any stale internal snapshot registration, adds one
/// provider-local immutable snapshot, validates declared component metadata,
/// and delegates provider construction to <see cref="DefaultServiceProviderFactory"/>.
/// </para>
/// <para>
/// No application service or registration factory is resolved during capture
/// or declared-graph validation. Each provider owns ordinary Microsoft DI
/// lifetimes and disposal exactly as it would with the default factory.
/// </para>
/// </remarks>
public sealed class AgentKitServiceProviderFactory: IServiceProviderFactory<IServiceCollection>
{
    private readonly DefaultServiceProviderFactory _inner;
    private readonly ILogger<AgentKitServiceProviderFactory> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumDerivedInfrastructureRegistrations;

    /// <summary>Initializes a factory with Microsoft DI's default validation options and no bootstrap log destination.</summary>
    /// <remarks>
    /// The factory owns no provider and no application service. Each returned
    /// root provider owns services it creates and must be disposed by its host.
    /// The default options leave Microsoft DI build and scope validation
    /// disabled, matching <see cref="ServiceProviderOptions"/> defaults.
    /// </remarks>
    public AgentKitServiceProviderFactory()
        : this(
            new ServiceProviderOptions(),
            NullLogger<AgentKitServiceProviderFactory>.Instance,
            TimeProvider.System,
            new AgentKitCompositionOptions())
    {
    }

    /// <summary>Initializes a factory with explicitly configured Microsoft DI validation behavior.</summary>
    /// <param name="options">The non-null provider options to capture at construction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>The option values are copied so later caller mutation cannot change provider builds performed by this factory.</remarks>
    public AgentKitServiceProviderFactory(ServiceProviderOptions options)
        : this(
            options,
            NullLogger<AgentKitServiceProviderFactory>.Instance,
            TimeProvider.System,
            new AgentKitCompositionOptions())
    {
    }

    /// <summary>Initializes a factory with Microsoft DI behavior and explicit bounded AgentKit composition validation.</summary>
    /// <param name="compositionOptions">The non-null immutable AgentKit validation limits to capture.</param>
    /// <param name="options">The non-null provider options to copy at construction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="compositionOptions"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>The caller retains the immutable options values. Every provider built by this factory captures the configured bound into its own registration snapshot.</remarks>
    public AgentKitServiceProviderFactory(
        AgentKitCompositionOptions compositionOptions,
        ServiceProviderOptions options)
        : this(
            options,
            NullLogger<AgentKitServiceProviderFactory>.Instance,
            TimeProvider.System,
            compositionOptions)
    {
    }

    /// <summary>Initializes a factory with captured Microsoft DI options and a directly supplied bootstrap logger.</summary>
    /// <param name="options">The non-null provider options to copy at construction.</param>
    /// <param name="logger">The non-null logger used without resolving application services before validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The caller owns the logger. Logging remains best effort and cannot
    /// change provider construction, rejection, or disposal. The factory owns
    /// no returned provider; the host must dispose every root it builds.
    /// </remarks>
    public AgentKitServiceProviderFactory(
        ServiceProviderOptions options,
        ILogger<AgentKitServiceProviderFactory> logger)
        : this(options, logger, TimeProvider.System, new AgentKitCompositionOptions())
    {
    }

    /// <summary>Initializes a factory with captured DI options, a bootstrap logger, and an observational clock.</summary>
    /// <param name="options">The non-null provider options to copy at construction.</param>
    /// <param name="logger">The non-null directly supplied bootstrap logger.</param>
    /// <param name="timeProvider">The non-null clock used only to measure provider-build duration.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/>, <paramref name="logger"/>, or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Logger or clock failure is observational and cannot change provider
    /// construction, rejection, or disposal. The caller owns both supplied
    /// services. The factory owns no returned provider.
    /// </remarks>
    public AgentKitServiceProviderFactory(
        ServiceProviderOptions options,
        ILogger<AgentKitServiceProviderFactory> logger,
        TimeProvider timeProvider)
        : this(options, logger, timeProvider, new AgentKitCompositionOptions())
    {
    }

    /// <summary>Initializes a factory with captured DI behavior, bootstrap observation, and bounded composition validation.</summary>
    /// <param name="options">The non-null provider options to copy at construction.</param>
    /// <param name="logger">The non-null directly supplied bootstrap logger.</param>
    /// <param name="timeProvider">The non-null clock used only to measure provider-build duration.</param>
    /// <param name="compositionOptions">The non-null immutable AgentKit validation limits to capture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>, <paramref name="logger"/>, <paramref name="timeProvider"/>, or <paramref name="compositionOptions"/> is <see langword="null"/>.</exception>
    /// <remarks>Logger or clock failure is observational. The caller owns supplied services and immutable options; the factory owns no returned provider.</remarks>
    public AgentKitServiceProviderFactory(
        ServiceProviderOptions options,
        ILogger<AgentKitServiceProviderFactory> logger,
        TimeProvider timeProvider,
        AgentKitCompositionOptions compositionOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(compositionOptions);
        _inner = new DefaultServiceProviderFactory(
            new ServiceProviderOptions
            {
                ValidateOnBuild = options.ValidateOnBuild,
                ValidateScopes = options.ValidateScopes,
            });
        _logger = logger;
        _timeProvider = timeProvider;
        _maximumDerivedInfrastructureRegistrations =
            compositionOptions.MaximumDerivedInfrastructureRegistrations;
    }

    /// <summary>Returns the host's mutable collection as the standard Microsoft DI container builder.</summary>
    /// <param name="services">The non-null collection the host will finish configuring before provider construction.</param>
    /// <returns>The exact same collection instance; this operation does not copy, inspect, or resolve registrations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>Snapshot capture occurs later in <see cref="CreateServiceProvider"/>, after the host has completed builder configuration.</remarks>
    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    /// <summary>Captures, validates, and builds one provider from the completed container builder.</summary>
    /// <param name="containerBuilder">The non-null completed Microsoft DI registration collection.</param>
    /// <returns>A new provider owning services created under the configured Microsoft DI lifetimes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="containerBuilder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="containerBuilder"/> contains a null service descriptor.</exception>
    /// <exception cref="AgentCompositionException">The declared closed component graph or its exact DI correspondence is invalid.</exception>
    /// <exception cref="AggregateException">Microsoft DI build validation finds one or more invalid constructor or lifetime graphs.</exception>
    /// <remarks>
    /// Descriptor validation precedes diagnostics and provider effects. The
    /// returned root owns services created by Microsoft DI and must be disposed
    /// by the host. Diagnostics use only the logger supplied at construction
    /// and process-wide Microsoft diagnostic sources; no application service
    /// is resolved before graph validation.
    /// </remarks>
    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        ArgumentNullException.ThrowIfNull(containerBuilder);
        var capturedServices = containerBuilder.ToImmutableArray();
        ArgumentException.ThrowIfContainsNull(capturedServices, nameof(containerBuilder));

        var (scope, timestamp) = AgentCompositionBuildObservability.Start(_timeProvider);
        try
        {
            var providerServices = CopyWithoutPriorSnapshot(capturedServices);
            ComponentRegistrationSnapshot? snapshot = null;
            _ = providerServices.AddSingleton(_ => snapshot
                ?? throw new InvalidOperationException("The provider-local component registration snapshot was not initialized."));
            snapshot = ComponentRegistrationSnapshot.Capture(
                providerServices,
                _maximumDerivedInfrastructureRegistrations);
            AgentCompositionValidator.ValidateComponentRegistrations(snapshot);
            var provider = _inner.CreateServiceProvider(providerServices);
            AgentCompositionBuildObservability.CompleteBuilt(scope, _timeProvider, timestamp, _logger);
            return provider;
        }
        catch (AgentCompositionException)
        {
            AgentCompositionBuildObservability.CompleteRejected(scope, _timeProvider, timestamp, _logger);
            throw;
        }
        catch (Exception exception)
        {
            AgentCompositionBuildObservability.CompleteFailed(
                scope, _timeProvider, timestamp, _logger, exception);
            throw;
        }
    }

    /// <summary>Copies the completed registration collection without stale provider-local snapshot evidence.</summary>
    /// <param name="services">The initialized, null-free descriptors captured at the provider boundary.</param>
    /// <returns>A mutable collection containing the exact descriptors that will be built except for newly added snapshot evidence.</returns>
    private static ServiceCollection CopyWithoutPriorSnapshot(ImmutableArray<ServiceDescriptor> services)
    {
        Debug.Assert(!services.IsDefault, "The public provider boundary captures an initialized descriptor array.");
        Debug.Assert(services.All(static descriptor => descriptor is not null),
            "The public provider boundary rejects null descriptors before copying.");
        var copy = new ServiceCollection();
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(ComponentRegistrationSnapshot))
            {
                _ = copy.Add(descriptor);
            }
        }

        return copy;
    }
}
