// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the deterministic in-memory
/// session store.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="InMemorySessionStore"/> as the singular
        /// <see cref="ISessionStore"/>, along with default GUID-based
        /// generators for <see cref="SessionId"/> and <see cref="BranchId"/>.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: uses <c>TryAdd</c> semantics, so calling this more
        /// than once, or alongside another store registration that already
        /// claimed <see cref="ISessionStore"/>, keeps whichever registration
        /// happened first.
        /// </remarks>
        public IServiceCollection AddInMemorySessionStore()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SessionId>>(
                _ => new GuidIdentifierGenerator<SessionId>(static value => new SessionId(value)));
            services.TryAddSingleton<IIdentifierGenerator<BranchId>>(
                _ => new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)));
            services.TryAddSingleton<ISessionStore, InMemorySessionStore>();

            return services;
        }
    }
}
