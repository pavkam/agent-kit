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
            _ = services.AddAgentKitObservability();
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
            return services;
        }

        /// <summary>Registers the first-party HTTPS search provider over one explicitly configured endpoint.</summary>
        /// <param name="configure">Required endpoint and provider options; no default endpoint is supplied.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <remarks>
        /// <para>
        /// The provider sends only through the registered <see cref="INetworkNameResolver"/> and
        /// <see cref="INetworkTransport"/>, under per-attempt resolution and send grants, and consumes the tool-issued
        /// egress grant through the registered <see cref="ISecurityGrantStore"/> and
        /// <see cref="ISecurityAuditDispatcher"/>. No <c>HttpClient</c> or handler is registered or created, and no
        /// fallback transport exists: when any of those collaborators is not registered, resolving
        /// <see cref="IWebSearchProvider"/> throws instead of sending unprotected.
        /// </para>
        /// <para>
        /// Registration is additive for options and uses <c>TryAdd</c> semantics for the provider, the replaceable
        /// <see cref="TimeProvider"/>, and the replaceable identity generators, so an earlier
        /// <see cref="IWebSearchProvider"/> registration wins and hosts replace any default through DI.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        public IServiceCollection AddNetworkWebSearchProvider(Action<NetworkWebSearchProviderOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            var optionsBuilder = services.AddOptions<NetworkWebSearchProviderOptions>()
                .Validate(
                    static value => value.Endpoint is
                    {
                        IsAbsoluteUri: true,
                        Scheme: "https",
                        UserInfo: "",
                        Fragment: "",
                    },
                    "Endpoint must be an absolute HTTPS URI without user information or a fragment.")
                .Validate(static value => value.MaximumResponseBytes > 0, "MaximumResponseBytes must be positive.")
                .Validate(static value => value.ConnectTimeout > TimeSpan.Zero, "ConnectTimeout must be positive.")
                .Validate(static value => Enum.IsDefined(value.Classification), "Classification must be a defined value.")
                .ValidateOnStart();
            _ = optionsBuilder.Configure(configure);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>>(
                new GuidIdentifierGenerator<SecurityRequestId>(static value => new SecurityRequestId(value)));
            services.TryAddSingleton<IIdentifierGenerator<NetworkOperationId>>(
                new GuidIdentifierGenerator<NetworkOperationId>(static value => new NetworkOperationId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
                new GuidIdentifierGenerator<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
            services.TryAddSingleton<IWebSearchProvider>(static provider => new NetworkWebSearchProvider(
                provider.GetRequiredService<ISecurityAuthoritySelector>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<INetworkNameResolver>(),
                provider.GetRequiredService<INetworkTransport>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityRequestId>>(),
                provider.GetRequiredService<IIdentifierGenerator<NetworkOperationId>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<NetworkWebSearchProviderOptions>>(),
                provider.GetService<ILogger<NetworkWebSearchProvider>>()));
            return services;
        }
    }
}
