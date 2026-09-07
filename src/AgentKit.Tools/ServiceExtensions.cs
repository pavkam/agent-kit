// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the built-in tool invocation pipeline.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the built-in tool catalog, allow-list authorizer, and
        /// invoker.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="AgentToolsOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c>
        /// semantics, so calling this more than once keeps the first
        /// registration. This method does not register any concrete
        /// <see cref="ITool"/>; use <see cref="AddTool{TTool}"/> to add
        /// each tool the application wants available.
        /// </remarks>
        public IServiceCollection AddAgentTools(Action<AgentToolsOptions>? configure = null)
        {
            var optionsBuilder = services.AddOptions<AgentToolsOptions>();
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton<IToolAuthorizer, AllowListToolAuthorizer>();
            services.TryAddSingleton<IToolCatalog>(static provider => new ToolCatalog(provider.GetServices<ITool>()));
            services.TryAddSingleton<IToolInvoker, DefaultToolInvoker>();

            return services;
        }

        /// <summary>Adds <typeparamref name="TTool"/> to the additive set of registered tools.</summary>
        /// <typeparam name="TTool">The tool implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddTool<TTool>()
            where TTool : class, ITool
        {
            _ = services.AddSingleton<ITool, TTool>();
            return services;
        }
    }
}
