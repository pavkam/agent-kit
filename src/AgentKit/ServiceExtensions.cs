// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Provides dependency-injection registration for the AgentKit facade and its
/// replaceable foundation defaults.
/// </summary>
/// <remarks>
/// These methods only modify the supplied service collection. They never build
/// a provider. Feature packages register loops, providers, sessions, tools, and
/// other runtime components independently against the same collection.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the host-managed AgentKit facade and its foundation clock.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Registration is idempotent and uses <c>TryAdd</c> semantics. The
        /// engine and clock are singular, thread-safe singletons. A resolved
        /// engine does not own the external host's provider; the host remains
        /// responsible for scopes, shutdown, and disposal. This scaffold does
        /// not register a loop, provider, store, tool, permission authority, or
        /// any other concrete agent component.
        /// </remarks>
        public IServiceCollection AddAgentKit()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(
                static provider => new AgentEngine(
                    provider.GetRequiredService<TimeProvider>(),
                    ownedProvider: null));

            return services;
        }

        /// <summary>
        /// Replaces the engine-wide clock with a caller-supplied instance.
        /// </summary>
        /// <param name="timeProvider">
        /// The non-null clock to capture in engines built or resolved after the
        /// replacement.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> or <paramref name="timeProvider"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// This is an explicit singular replacement: all existing
        /// <see cref="TimeProvider"/> descriptors are removed before the
        /// supplied instance is registered. As with Microsoft DI instance
        /// registrations, the caller retains disposal ownership of the
        /// instance. Existing engines retain their previously captured clock.
        /// </remarks>
        public IServiceCollection ReplaceTimeProvider(TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(timeProvider);

            _ = services.RemoveAll<TimeProvider>();
            _ = services.AddSingleton(timeProvider);
            return services;
        }
    }
}
