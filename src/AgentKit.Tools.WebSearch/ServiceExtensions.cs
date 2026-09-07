// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Registers provider-backed web search without fabricating a provider, endpoint, or credential.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the search tool, request identity source, and validated host ceilings.</summary>
        /// <param name="configure">Optional search and projection bounds.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddWebSearchTool(Action<WebSearchToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<WebSearchToolOptions>()
                .Validate(static value => value.MaximumQueryCharacters > 0, "MaximumQueryCharacters must be positive.")
                .Validate(static value => value.MaximumDomains >= 0, "MaximumDomains must not be negative.")
                .Validate(static value => value.DefaultMaximumResults > 0, "DefaultMaximumResults must be positive.")
                .Validate(static value => value.MaximumResults >= value.DefaultMaximumResults, "MaximumResults must not be less than the default.")
                .Validate(static value => value.DefaultTimeout > TimeSpan.Zero, "DefaultTimeout must be positive.")
                .Validate(static value => value.MaximumTimeout >= value.DefaultTimeout, "MaximumTimeout must not be less than the default.")
                .Validate(static value => value.MaximumTitleCharacters > 0, "MaximumTitleCharacters must be positive.")
                .Validate(static value => value.MaximumSnippetCharacters > 0, "MaximumSnippetCharacters must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<WebSearchRequestId>, GuidWebSearchRequestIdGenerator>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, WebSearchTool>());
            return services;
        }
    }
}
