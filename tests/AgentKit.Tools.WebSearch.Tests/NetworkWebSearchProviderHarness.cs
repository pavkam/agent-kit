// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

using System.Net.Http;

using AgentKit.TestSupport;

/// <summary>Composes <see cref="NetworkWebSearchProvider"/> over deterministic security and network fakes.</summary>
internal sealed class NetworkWebSearchProviderHarness
{
    private const string Endpoint = "https://search.example.test/query";

    private NetworkWebSearchProviderHarness(
        NetworkWebSearchProvider provider,
        ConsumingGrantStore grants,
        GrantingSecurityAuthority authority,
        RecordingAuditDispatcher audit,
        FixedAddressNameResolver resolver,
        INetworkTransport transport,
        RecordingLogger<NetworkWebSearchProvider> logger)
    {
        Provider = provider;
        Grants = grants;
        Authority = authority;
        Audit = audit;
        Resolver = resolver;
        Transport = transport;
        Logger = logger;
    }

    internal NetworkWebSearchProvider Provider { get; }

    internal ConsumingGrantStore Grants { get; }

    internal GrantingSecurityAuthority Authority { get; }

    internal RecordingAuditDispatcher Audit { get; }

    internal FixedAddressNameResolver Resolver { get; }

    internal INetworkTransport Transport { get; }

    internal RecordingLogger<NetworkWebSearchProvider> Logger { get; }

    internal IReadOnlyList<NetworkRequest> Requests => Transport switch
    {
        HandlerNetworkTransport handler => handler.Requests,
        CallbackNetworkTransport callback => callback.Requests,
        _ => [],
    };

    internal static NetworkWebSearchProviderHarness Create(
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null,
        Action<NetworkWebSearchProviderOptions>? configure = null,
        ISecurityAuthoritySelector? selector = null) =>
        Create(grants => new HandlerNetworkTransport(handler, grants), timeProvider, configure, selector);

    internal static NetworkWebSearchProviderHarness Create(
        Func<ISecurityGrantStore, INetworkTransport> transportFactory,
        TimeProvider? timeProvider = null,
        Action<NetworkWebSearchProviderOptions>? configure = null,
        ISecurityAuthoritySelector? selector = null)
    {
        var clock = timeProvider ?? new FixedTimeProvider();
        var grants = new ConsumingGrantStore();
        var authority = new GrantingSecurityAuthority(grants);
        var audit = new RecordingAuditDispatcher();
        var resolver = new FixedAddressNameResolver(grants, clock);
        var transport = transportFactory(grants);
        var logger = new RecordingLogger<NetworkWebSearchProvider>();
        var options = new NetworkWebSearchProviderOptions { Endpoint = new Uri(Endpoint) };
        configure?.Invoke(options);
        var provider = new NetworkWebSearchProvider(
            selector ?? new FixedSecurityAuthoritySelector(authority),
            grants,
            audit,
            resolver,
            transport,
            new SequentialGenerator<SecurityRequestId>(static value => new SecurityRequestId(value), "20000000"),
            new SequentialGenerator<NetworkOperationId>(static value => new NetworkOperationId(value), "31000000"),
            new SequentialGenerator<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value), "41000000"),
            new SequentialGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value), "51000000"),
            clock,
            Options.Create(options),
            logger);
        return new NetworkWebSearchProviderHarness(provider, grants, authority, audit, resolver, transport, logger);
    }

    /// <summary>Issues the tool-side egress grant exactly as <c>WebSearchTool</c> does, through the harness authority.</summary>
    internal async Task<WebSearchRequest> AuthorizedRequestAsync(
        string query = "agentkit",
        ImmutableArray<NormalizedHost> domains = default,
        WebSearchFreshness freshness = WebSearchFreshness.Any,
        int maximumResults = 5,
        DateTimeOffset? deadline = null)
    {
        var context = TestData.Context;
        var effectiveDomains = domains.IsDefault ? [] : domains;
        var effectiveDeadline = deadline ?? DateTimeOffset.UnixEpoch.AddMinutes(1);
        var fingerprint = WebSearchSecurityBinding.Fingerprint(
            TestData.SearchId,
            Provider.ProviderId,
            Provider.Destination,
            query,
            effectiveDomains,
            freshness,
            maximumResults,
            effectiveDeadline);
        var decision = await Authority.AuthorizeAsync(
            new SecurityRequest(
                new SecurityRequestId(Guid.Parse("21000000-0000-0000-0000-000000000001")),
                context.Authorization.Scope,
                context.ToolCallId,
                context.Authorization.Identity,
                context.Authorization,
                Provider.SecurityAudience,
                SecurityOperationKind.Network,
                SecurityEffect.Egress,
                [Provider.Destination],
                fingerprint,
                effectiveDeadline),
            TestContext.Current.CancellationToken);
        var grant = decision.ShouldBeOfType<SecurityAllowed>().Grant;
        return new WebSearchRequest(
            TestData.SearchId,
            context,
            query,
            effectiveDomains,
            freshness,
            maximumResults,
            effectiveDeadline,
            grant);
    }

    private sealed class SequentialGenerator<TId>(Func<Guid, TId> factory, string prefix): IIdentifierGenerator<TId>
        where TId : struct
    {
        private int _value;

        public TId Create() => factory(Guid.Parse($"{prefix}-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }
}
