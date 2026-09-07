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
    /// Validation happens before the engine is returned and before any agent
    /// runs. It checks that the engine-wide singular services exist, that at
    /// least one runnable agent definition is published, and that every
    /// definition resolves the collaborators a run needs. Missing behavior is
    /// a composition error, never a cue to instantiate a hidden default.
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
    /// The composition is missing a required engine-wide service, publishes no
    /// runnable agent definition, or contains a definition whose collaborators
    /// cannot be resolved.
    /// </exception>
    public AgentEngine Build()
    {
        ServiceProvider? provider = null;

        try
        {
            provider = Services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });

            AgentCompositionValidator.Validate(provider);
            return new AgentEngine(provider, provider);
        }
        catch
        {
            provider?.Dispose();
            throw;
        }
    }
}
