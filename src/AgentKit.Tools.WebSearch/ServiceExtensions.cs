// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

using AgentKit.Tools;

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
            _ = services.AddToolInvoker<WebSearchTool>(WebSearchTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == WebSearchTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(WebSearchTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, WebSearchTool>());
            return services;
        }

        /// <summary>Registers the first-party HTTPS search provider over one explicitly configured endpoint.</summary>
        /// <param name="configure">Required endpoint and provider options; no default endpoint is supplied.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">The endpoint is missing or invalid.</exception>
        public IServiceCollection AddNetworkWebSearchProvider(Action<NetworkWebSearchProviderOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            var optionsBuilder = services.AddOptions<NetworkWebSearchProviderOptions>()
                .Validate(
                    static value => value.Endpoint is { IsAbsoluteUri: true } && value.Endpoint.Scheme == "https",
                    "Endpoint must be an absolute HTTPS URI.")
                .Validate(static value => value.MaximumResponseBytes > 0, "MaximumResponseBytes must be positive.")
                .ValidateOnStart();
            _ = optionsBuilder.Configure(configure);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IWebSearchProvider>(static provider =>
            {
                var options = provider.GetRequiredService<IOptions<NetworkWebSearchProviderOptions>>().Value;
                var handler = new SocketsHttpHandler
                {
                    ConnectTimeout = options.ConnectTimeout,
                };
                var client = new HttpClient(handler, disposeHandler: true)
                {
                    Timeout = Timeout.InfiniteTimeSpan,
                };
                return new NetworkWebSearchProvider(
                    client,
                    options,
                    provider.GetRequiredService<TimeProvider>());
            });
            return services;
        }
    }
}
