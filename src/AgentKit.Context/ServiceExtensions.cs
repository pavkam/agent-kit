// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the built-in, reduced-scope
/// context assembler.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the built-in <see cref="DefaultContextAssembler"/>.</summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: this registration uses <c>TryAdd</c> semantics, so
        /// calling this more than once keeps the first registration.
        /// </remarks>
        public IServiceCollection AddAgentContext()
        {
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton<IContextAssembler, DefaultContextAssembler>();
            return services;
        }
    }
}
