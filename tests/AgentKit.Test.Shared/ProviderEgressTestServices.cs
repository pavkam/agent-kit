// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Net.Http;

using AgentKit.Providers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers a deterministic security and network composition so provider adapters resolve without sockets.</summary>
public static class ProviderEgressTestServices
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Replaces the registered resolver and transport with deterministic ones that consume their grants from the
        /// composition's own <see cref="ISecurityGrantStore"/> and answer through <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">The handler script every provider send is answered by.</param>
        /// <returns>The same collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="handler"/> is null.</exception>
        /// <remarks>
        /// Use this over a full engine composition whose security authority and grant store are real: the replaced
        /// boundaries still validate and consume the exact grants provider egress obtained from that authority, so the
        /// composition proves grant flow end to end without a socket.
        /// </remarks>
        public IServiceCollection ReplaceNetworkWithHandler(HttpMessageHandler handler)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(handler);
            _ = services.RemoveAll<INetworkNameResolver>();
            _ = services.AddSingleton<INetworkNameResolver>(static provider => new FixedAddressNameResolver(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>()));
            _ = services.RemoveAll<INetworkTransport>();
            _ = services.AddSingleton<INetworkTransport>(provider => new HandlerNetworkTransport(
                handler,
                provider.GetRequiredService<ISecurityGrantStore>()));
            return services;
        }

        /// <summary>
        /// Registers <c>AddAgentProviders</c> plus a granting authority, strict grant store, recording audit dispatcher,
        /// fixed-address resolver, and a handler-backed transport, replacing any earlier transport or resolver.
        /// </summary>
        /// <param name="handler">
        /// The handler script the transport answers through. When omitted, any send fails the test because the
        /// registration is only meant to let adapter graphs resolve.
        /// </param>
        /// <returns>The same collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddProviderEgressTestServices(HttpMessageHandler? handler = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentProviders();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ConsumingGrantStore>();
            services.TryAddSingleton<ISecurityGrantStore>(static provider => provider.GetRequiredService<ConsumingGrantStore>());
            services.TryAddSingleton(static provider => new GrantingSecurityAuthority(provider.GetRequiredService<ConsumingGrantStore>()));
            services.TryAddSingleton<ISecurityAuthoritySelector>(
                static provider => new FixedSecurityAuthoritySelector(provider.GetRequiredService<GrantingSecurityAuthority>()));
            services.TryAddSingleton<RecordingAuditDispatcher>();
            services.TryAddSingleton<ISecurityAuditDispatcher>(static provider => provider.GetRequiredService<RecordingAuditDispatcher>());
            _ = services.RemoveAll<INetworkNameResolver>();
            _ = services.AddSingleton<INetworkNameResolver>(static provider => new FixedAddressNameResolver(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>()));
            _ = services.RemoveAll<INetworkTransport>();
            _ = services.AddSingleton<INetworkTransport>(provider => new HandlerNetworkTransport(
                handler ?? new UnreachableHandler(),
                provider.GetRequiredService<ISecurityGrantStore>()));
            return services;
        }
    }

    private sealed class UnreachableHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The test composition was not given a handler, so no provider send is expected.");
    }
}
