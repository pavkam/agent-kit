// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Net.Http;

using AgentKit.Providers.Credentials;
using AgentKit.Providers.Egress;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>A complete deterministic provider-egress composition for adapter tests.</summary>
/// <remarks>
/// The harness wires the real <see cref="ProviderEgress"/> over a granting authority, a strict grant store, a
/// recording audit dispatcher, a fixed-address resolver, and a transport, so every adapter test exercises the
/// provider-egress, resolution, and send grants end to end without any HTTP client or socket.
/// </remarks>
public sealed class ProviderEgressHarness
{
    private ProviderEgressHarness(
        ProviderEgress egress,
        ConsumingGrantStore grants,
        GrantingSecurityAuthority authority,
        RecordingAuditDispatcher audit,
        FixedAddressNameResolver resolver,
        INetworkTransport transport,
        ProviderCredentialReadGate gate)
    {
        Egress = egress;
        Grants = grants;
        Authority = authority;
        Audit = audit;
        Resolver = resolver;
        Transport = transport;
        Gate = gate;
    }

    /// <summary>Gets the egress boundary under test.</summary>
    public ProviderEgress Egress { get; }

    /// <summary>Gets the strict grant store every boundary consumes from.</summary>
    public ConsumingGrantStore Grants { get; }

    /// <summary>Gets the authority that records and answers every security request.</summary>
    public GrantingSecurityAuthority Authority { get; }

    /// <summary>Gets the audit dispatcher grant consumption must reach.</summary>
    public RecordingAuditDispatcher Audit { get; }

    /// <summary>Gets the resolver that proves which destinations were resolved.</summary>
    public FixedAddressNameResolver Resolver { get; }

    /// <summary>Gets the transport the egress sends through.</summary>
    public INetworkTransport Transport { get; }

    /// <summary>Gets the credential-read gate over the same grant store and audit dispatcher the egress uses.</summary>
    /// <value>A gate first-party credential sources enforce credential-read grants through in tests.</value>
    public ProviderCredentialReadGate Gate { get; }

    /// <summary>Gets the protected operation context that selects the harness authority.</summary>
    /// <value>A stable service-subject operation shared by all adapter tests.</value>
    public static ProtectedSemanticOperationContext Operation { get; } = CreateOperation();

    /// <summary>Binds a conversational descriptor to the standard test endpoint and credential profile binding.</summary>
    /// <param name="descriptor">The descriptor to bind.</param>
    /// <returns>The descriptor with <see cref="StaticProviderProfileRuntimeSelector.Binding"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public static ModelDescriptor Bind(ModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with { Binding = StaticProviderProfileRuntimeSelector.Binding };
    }

    /// <summary>Binds an embedding descriptor to the standard test endpoint and credential profile binding.</summary>
    /// <param name="descriptor">The descriptor to bind.</param>
    /// <returns>The descriptor with <see cref="StaticProviderProfileRuntimeSelector.Binding"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public static EmbeddingModelDescriptor Bind(EmbeddingModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with { Binding = StaticProviderProfileRuntimeSelector.Binding };
    }

    /// <summary>Binds a reranker descriptor to the standard test endpoint and credential profile binding.</summary>
    /// <param name="descriptor">The descriptor to bind.</param>
    /// <returns>The descriptor with <see cref="StaticProviderProfileRuntimeSelector.Binding"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public static RerankerDescriptor Bind(RerankerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with { Binding = StaticProviderProfileRuntimeSelector.Binding };
    }

    /// <summary>Creates a harness whose transport answers through <paramref name="handler"/>.</summary>
    /// <param name="handler">The handler script; it is borrowed, not disposed.</param>
    /// <param name="timeProvider">The clock shared with the adapter under test, or the system clock when omitted.</param>
    /// <param name="configure">An optional options customization.</param>
    /// <param name="logger">An optional logger that records the egress boundary's content-free events.</param>
    /// <returns>The composed harness.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static ProviderEgressHarness Create(
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null,
        Action<ProviderEgressOptions>? configure = null,
        ILogger<ProviderEgress>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var grants = new ConsumingGrantStore();
        return Compose(grants, new HandlerNetworkTransport(handler, grants), timeProvider ?? TimeProvider.System, configure, logger);
    }

    /// <summary>Creates a harness over an explicit transport that consumes grants from the returned harness store.</summary>
    /// <param name="transportFactory">Builds the transport from the harness grant store.</param>
    /// <param name="timeProvider">The clock shared with the adapter under test, or the system clock when omitted.</param>
    /// <param name="configure">An optional options customization.</param>
    /// <param name="logger">An optional logger that records the egress boundary's content-free events.</param>
    /// <returns>The composed harness.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transportFactory"/> is null.</exception>
    public static ProviderEgressHarness Create(
        Func<ISecurityGrantStore, INetworkTransport> transportFactory,
        TimeProvider? timeProvider = null,
        Action<ProviderEgressOptions>? configure = null,
        ILogger<ProviderEgress>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transportFactory);
        var grants = new ConsumingGrantStore();
        return Compose(grants, transportFactory(grants), timeProvider ?? TimeProvider.System, configure, logger);
    }

    private static ProviderEgressHarness Compose(
        ConsumingGrantStore grants,
        INetworkTransport transport,
        TimeProvider timeProvider,
        Action<ProviderEgressOptions>? configure,
        ILogger<ProviderEgress>? logger)
    {
        var authority = new GrantingSecurityAuthority(grants);
        var audit = new RecordingAuditDispatcher();
        var resolver = new FixedAddressNameResolver(grants, timeProvider);
        var options = new ProviderEgressOptions();
        configure?.Invoke(options);
        var egress = new ProviderEgress(
            new FixedSecurityAuthoritySelector(authority),
            grants,
            audit,
            resolver,
            transport,
            new SequentialGenerator<SecurityRequestId>(value => new SecurityRequestId(value), "20000000"),
            new SequentialGenerator<NetworkOperationId>(value => new NetworkOperationId(value), "30000000"),
            new SequentialGenerator<SecurityEnforcementIntentId>(value => new SecurityEnforcementIntentId(value), "40000000"),
            new SequentialGenerator<SecurityAuditRecordId>(value => new SecurityAuditRecordId(value), "50000000"),
            timeProvider,
            Options.Create(options),
            logger);
        var gate = new ProviderCredentialReadGate(
            grants,
            audit,
            new SequentialGenerator<SecurityEnforcementIntentId>(value => new SecurityEnforcementIntentId(value), "41000000"),
            new SequentialGenerator<SecurityAuditRecordId>(value => new SecurityAuditRecordId(value), "51000000"),
            timeProvider);
        return new ProviderEgressHarness(egress, grants, authority, audit, resolver, transport, gate);
    }

    private static ProtectedSemanticOperationContext CreateOperation()
    {
        var agentId = new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("a0000000-0000-0000-0000-000000000002"));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("a0000000-0000-0000-0000-000000000003")),
            admissionId: null);
        return new ProtectedSemanticOperationContext(
            agentId,
            sessionId,
            conversationId: null,
            identity,
            correlation,
            TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity));
    }

    private sealed class SequentialGenerator<TId>(Func<Guid, TId> factory, string prefix): IIdentifierGenerator<TId>
        where TId : struct
    {
        private int _value;

        public TId Create() => factory(Guid.Parse($"{prefix}-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }
}
