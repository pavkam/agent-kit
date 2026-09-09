// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Collects service registrations for one standalone <see cref="AgentEngine"/>.
/// </summary>
/// <remarks>
/// The builder is mutable and is not thread-safe. Each successful
/// <see cref="Build"/> captures the registrations as an immutable Microsoft DI
/// service provider owned by the returned engine. Later changes to
/// <see cref="Services"/> do not reconfigure an engine that was already built.
/// </remarks>
public sealed class AgentEngineBuilder
{
    /// <summary>
    /// Initializes a builder with the AgentKit facade defaults.
    /// </summary>
    public AgentEngineBuilder()
    {
        Services = new ServiceCollection();
        _ = Services.AddAgentKit();
    }

    /// <summary>
    /// Gets the ordinary Microsoft dependency-injection composition surface.
    /// </summary>
    /// <value>
    /// A mutable collection owned by this builder. Feature packages and
    /// applications add, replace, or remove registrations through their public
    /// <see cref="IServiceCollection"/> extension methods before building.
    /// </value>
    public IServiceCollection Services { get; }

    /// <summary>Gets or sets immutable bounds applied when this builder validates its next standalone composition.</summary>
    /// <value>Non-null composition options. The default uses <see cref="AgentKitCompositionOptions.DefaultMaximumDerivedInfrastructureRegistrations"/>; assigning a new value affects later builds only.</value>
    /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
    public AgentKitCompositionOptions CompositionOptions
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = new();

    /// <summary>
    /// Validates and captures the current service registrations in a new
    /// standalone engine.
    /// </summary>
    /// <returns>
    /// An immutable engine that owns the service provider created for this
    /// build. The caller must asynchronously dispose the engine.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Declared component metadata is frozen and its pure graph and Microsoft
    /// DI correspondence are validated before a provider is built, so an
    /// invalid declaration cannot run a registration factory. Reduced
    /// readiness validation then checks that engine-wide singular services
    /// exist, at least one runnable definition is published, and exactly one
    /// loop is registered for the current reduced runtime. This structural
    /// readiness check never activates the loop or its collaborators. Missing
    /// behavior is a composition error, never a cue to instantiate a hidden
    /// default. Component metadata remains explicitly partial until the staged
    /// owner rollout declares the complete runnable spine.
    /// </para>
    /// <para>
    /// When validation fails, the partially built provider is disposed before
    /// the exception propagates, so a failed build leaks nothing.
    /// </para>
    /// </remarks>
    /// <exception cref="AggregateException">
    /// One or more service registrations form an invalid constructor or scope
    /// graph.
    /// </exception>
    /// <exception cref="AgentCompositionException">
    /// The declared component graph or its Microsoft DI correspondence is
    /// invalid, the composition is missing a required engine-wide service,
    /// publishes no runnable agent definition, or contains a definition whose
    /// collaborators cannot be resolved.
    /// </exception>
    public AgentEngine Build()
    {
        ServiceProvider? provider = null;

        try
        {
            var factory = new AgentKitServiceProviderFactory(
                CompositionOptions,
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });
            provider = (ServiceProvider) factory.CreateServiceProvider(factory.CreateBuilder(Services));

            var composition = AgentCompositionValidator.Validate(provider);
            return new AgentEngine(provider, provider, composition);
        }
        catch
        {
            provider?.Dispose();
            throw;
        }
    }
}
