// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using Microsoft.Extensions.Options;

using TestSupport;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies the core registration is idempotent and leaves grant storage to an explicit adapter.</summary>
    [Fact]
    public void AddAgentPermissions_WhenCalledTwice_RegistersOneRuntimeAndNoGrantStore()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        provider.GetServices<ISecurityGrantStore>().ShouldBeEmpty();
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityAuthority)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityPolicyCatalog)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityPolicySelector)).ShouldBe(1);
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    /// <summary>Verifies selecting the explicit in-memory adapter completes the core authority composition.</summary>
    [Fact]
    public void AddAgentPermissions_WhenExplicitInMemoryStoreIsSelected_ResolvesIssuingAuthority()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddInMemoryApprovalStore();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ISecurityAuthority>().ShouldBeOfType<SecurityAuthority>();
    }

    /// <summary>Verifies incomplete security composition fails while resolving the issuing authority before any protected operation.</summary>
    [Fact]
    public void AddAgentPermissions_WhenNoGrantStoreIsSelected_FailsBeforeAuthorityCanIssueGrants()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ISecurityAuthority>);
        exception.Message.ShouldContain(nameof(ISecurityGrantStore));
    }

    /// <summary>Verifies hosts that do not select approval storage can validate unrelated permission services.</summary>
    [Fact]
    public void AddAgentPermissions_WhenNoApprovalStoreIsSelected_AllowsValidationUntilApprovalBrokerIsRequested()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<IApprovalBroker>);
        exception.Message.ShouldContain(nameof(IApprovalStore));
    }

    [Fact]
    public async Task AddSecurityProfilePublication_WhenRegistrationsAreDistinct_ResolvesEachExactPublication()
    {
        var first = Publication();
        var second = Publication(profileKey: new SecurityProfileKey("security.other"));
        var services = new ServiceCollection();
        _ = services.AddSecurityProfilePublication(first);
        _ = services.AddAgentPermissions();
        _ = services.AddSecurityProfilePublication(second);
        using var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<ISecurityProfilePublicationReader>();
        var firstResult = await reader.ReadAsync(first.AgentId, first.AgentDefinitionRevision, first.ConfigurationVersion, first.ProfileKey, TestContext.Current.CancellationToken);
        var secondResult = await reader.ReadAsync(second.AgentId, second.AgentDefinitionRevision, second.ConfigurationVersion, second.ProfileKey, TestContext.Current.CancellationToken);
        firstResult.ShouldBeOfType<SecurityProfilePublicationFound>().Publication.ShouldBeSameAs(first);
        secondResult.ShouldBeOfType<SecurityProfilePublicationFound>().Publication.ShouldBeSameAs(second);
    }

    [Fact]
    public void AddSecurityProfilePublication_WhenExactCoordinatesAreDuplicated_FailsWhenReaderFreezesComposition()
    {
        var publication = Publication();
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddSecurityProfilePublication(publication);
        _ = services.AddSecurityProfilePublication(Publication(profileVersion: new SecurityProfileVersion(99)));
        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<ArgumentException>(provider.GetRequiredService<ISecurityProfilePublicationReader>);
        exception.ParamName.ShouldBe("publications");
    }

    [Fact]
    public void AddSecurityProfilePublication_WhenArgumentsAreInvalid_DoesNotMutateServices()
    {
        var services = new ServiceCollection();
        var nullServices = Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddSecurityProfilePublication(null!, Publication()));
        var nullPublication = Should.Throw<ArgumentNullException>(() => services.AddSecurityProfilePublication(null!));
        nullServices.ParamName.ShouldBe("services");
        nullPublication.ParamName.ShouldBe("publication");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddAgentPermissions_WhenCaptureAxesAreHostSupplied_PreservesReplaceableSingletons()
    {
        var reader = new StubPublicationReader();
        var selector = new StubProfileSelector();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityProfilePublicationReader>(reader);
        _ = services.AddSingleton<ISecurityProfileSelector>(selector);
        _ = services.AddAgentPermissions();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        provider.GetServices<ISecurityProfilePublicationReader>().ShouldHaveSingleItem().ShouldBeSameAs(reader);
        provider.GetServices<ISecurityProfileSelector>().ShouldHaveSingleItem().ShouldBeSameAs(selector);
        provider.GetRequiredService<ISecurityProfilePublicationReader>().ShouldBeSameAs(reader);
        provider.GetRequiredService<ISecurityProfileSelector>().ShouldBeSameAs(selector);
    }

    private static SecurityProfilePublication Publication(SecurityProfileKey? profileKey = null, SecurityProfileVersion? profileVersion = null) => new(AgentId(), DefinitionRevision(), ConfigurationVersion(), profileKey ?? ProfileKey(), profileVersion ?? new SecurityProfileVersion(4), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("b2222222-2222-2222-2222-222222222222")), new SecurityPolicyVersion(5), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority.primary"));
    private static AgentId AgentId() => new(Guid.Parse("b1111111-1111-1111-1111-111111111111"));
    private static AgentDefinitionRevision DefinitionRevision() => new(2);
    private static ConfigurationVersion ConfigurationVersion() => new(3);
    private static SecurityProfileKey ProfileKey() => new("security.primary");
    private sealed class StubPublicationReader: ISecurityProfilePublicationReader
    {
        public ValueTask<SecurityProfilePublicationResult> ReadAsync(AgentId agentId, AgentDefinitionRevision agentDefinitionRevision, ConfigurationVersion configurationVersion, SecurityProfileKey profileKey, CancellationToken cancellationToken = default) => ValueTask.FromResult<SecurityProfilePublicationResult>(new SecurityProfilePublicationUnavailable("Unavailable."));
    }

    private sealed class StubProfileSelector: ISecurityProfileSelector
    {
        public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(SecurityAuthorizationCaptureRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptureUnavailable("Unavailable."));
    }

    [Fact]
    public async Task AddAgentPermissions_WhenAuditDispatcherIsUnconfigured_RegistersTheDefaultRequiredDispatcher()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ISecurityAuditDispatcher>();
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = dispatcher.ShouldBeOfType<DefaultSecurityAuditDispatcher>();
        _ = result.ShouldBeOfType<SecurityAuditUnavailable>();
    }

    [Fact]
    public void AddAgentPermissions_WhenAuditDeliveryTimeoutIsInvalid_RejectsItBeforeDispatcherActivation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions(options => options.AuditDeliveryTimeout = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<OptionsValidationException>(provider.GetRequiredService<ISecurityAuditDispatcher>);
        exception.GetType().ShouldBe(typeof(OptionsValidationException));
    }

    [Fact]
    public void AddAgentPermissions_WhenAuditDispatcherIsHostSupplied_PreservesTheReplacement()
    {
        var replacement = new FixedAuditDispatcher();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityAuditDispatcher>(replacement);
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISecurityAuditDispatcher>().ShouldBeSameAs(replacement);
    }

    [Fact]
    public async Task AddSecurityAuditSink_WhenCalledMoreThanOnce_BindsAllAdditiveSinks()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.Required, providesDurableAcceptance: true);
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddSecurityAuditSink(registration, first);
        _ = services.AddSecurityAuditSink(registration, second);
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISecurityAuditDispatcher>().DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        _ = first.Records.ShouldHaveSingleItem();
        _ = second.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddSecurityAuditSink_WhenArgumentsAreInvalid_ThrowsBeforeMutatingTheCollection()
    {
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.Required, providesDurableAcceptance: true);
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        AssertExact<ArgumentNullException>(() => ServiceExtensions.AddSecurityAuditSink(null!, registration, sink), "services");
        services.ShouldBeEmpty();
        AssertExact<ArgumentNullException>(() => services.AddSecurityAuditSink(null!, sink), "registration");
        services.ShouldBeEmpty();
        AssertExact<ArgumentNullException>(() => services.AddSecurityAuditSink(registration, null!), "sink");
        services.ShouldBeEmpty();
    }

    private static SecurityAuditRecord Record(SecurityRequestId? requestId = null, GrantId? grantId = null, ApprovalRequestId? approvalRequestId = null, SecurityPolicyVersion? policyVersion = null, IEnumerable<KeyValuePair<string, RedactedAuditValue>>? fields = null) => new(new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")), new SecurityAuthorizationScope(new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")), new SessionId(Guid.Parse("a3333333-3333-3333-3333-333333333333")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("a4444444-4444-4444-4444-444444444444")), new AdmissionId(Guid.Parse("a5555555-5555-5555-5555-555555555555")))), requestId ?? new SecurityRequestId(Guid.Parse("a6666666-6666-6666-6666-666666666666")), grantId ?? new GrantId(Guid.Parse("a7777777-7777-7777-7777-777777777777")), approvalRequestId, SecurityAuditEventKind.GrantConsumptionIntent, SecurityAuditOutcome.Accepted, policyVersion ?? new SecurityPolicyVersion(1), fields?.ToImmutableDictionary() ?? [], DateTimeOffset.UnixEpoch);
    private static void AssertExact<TException>(Action action, string parameterName)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }

    private static void AssertExact<TException>(Func<object?> factory, string parameterName)
        where TException : ArgumentException => AssertExact<TException>(() =>
    {
        _ = factory();
    }, parameterName);
    private sealed class RecordingSink: ISecurityAuditSink
    {
        public List<SecurityAuditRecord> Records { get; } = [];

        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) => ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    [Fact]
    public async Task AddAgentPermissions_WhenPublicationIsRegistered_ResolvesSingletonDefaultCaptureAxes()
    {
        var request = Request();
        var services = new ServiceCollection();
        _ = services.AddSecurityProfilePublication(PublicationDefaultSecurityProfileSelector(request));
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        var firstReader = provider.GetRequiredService<ISecurityProfilePublicationReader>();
        var secondReader = provider.GetRequiredService<ISecurityProfilePublicationReader>();
        var firstSelector = provider.GetRequiredService<ISecurityProfileSelector>();
        var secondSelector = provider.GetRequiredService<ISecurityProfileSelector>();
        var result = await firstSelector.SelectAsync(request, TestContext.Current.CancellationToken);
        firstReader.ShouldBeSameAs(secondReader);
        firstSelector.ShouldBeSameAs(secondSelector);
        _ = result.ShouldBeOfType<SecurityAuthorizationCaptured>();
    }

    private static SecurityAuthorizationCaptureRequest Request() => new(new SecurityAuthorizationScope(new AgentId(Guid.Parse("c1111111-1111-1111-1111-111111111111")), new SessionId(Guid.Parse("c2222222-2222-2222-2222-222222222222")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("c3333333-3333-3333-3333-333333333333")), new AdmissionId(Guid.Parse("c4444444-4444-4444-4444-444444444444")))), new SecurityProfileKey("security.primary"), new AgentDefinitionRevision(2), new ConfigurationVersion(3), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
    private static SecurityProfilePublication PublicationDefaultSecurityProfileSelector(SecurityAuthorizationCaptureRequest request, AgentId? agentId = null, AgentDefinitionRevision? definitionRevision = null, ConfigurationVersion? configurationVersion = null, SecurityProfileKey? profileKey = null, string fingerprint = "sha256:policy") => new(agentId ?? request.Scope.AgentId, definitionRevision ?? request.AgentDefinitionRevision, configurationVersion ?? request.ConfigurationVersion, profileKey ?? request.ProfileKey, new SecurityProfileVersion(4), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("c5555555-5555-5555-5555-555555555555")), new SecurityPolicyVersion(5), new ContentHash(fingerprint)), new ComponentKey<ISecurityAuthority>("authority.primary"));
    [Fact]
    public void AddSecurityAuthority_WhenArgumentsAreInvalid_ThrowsWithExactParameterNames()
    {
        var services = new ServiceCollection();
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(() => ServiceExtensions.AddSecurityAuthority(null!, new ComponentKey<ISecurityAuthority>("security.primary"), new DenyAllSecurityAuthority()), "services");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(() => services.AddSecurityAuthority(default, new DenyAllSecurityAuthority()), "authorityKey");
        services.ShouldBeEmpty();
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(() => services.AddSecurityAuthority(new ComponentKey<ISecurityAuthority>("security.primary"), null!), "authority");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddSecurityAuthority_WhenExplicitlyBound_RegistersItForExactSelectorResolution()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.host-owned");
        var authority = new DenyAllSecurityAuthority();
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddSecurityAuthority(key, authority);
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<ISecurityAuthoritySelector>();
        var result = await selector.SelectAsync(Context(key), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<SecurityAuthoritySelected>().Authority.ShouldBeSameAs(authority);
    }

    [Fact]
    public async Task AddAgentPermissions_WhenNoAuthorityBindingIsAdded_DoesNotSelectItsLegacyUnkeyedAuthority()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISecurityAuthoritySelector>().SelectAsync(Context(new ComponentKey<ISecurityAuthority>("security.unbound")), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthoritySelectionUnavailable>();
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityAuthority)).ShouldBe(1);
    }

    [Fact]
    public void AddSecurityAuthority_WhenGivenOnlyAKeyAndArgumentsAreInvalid_ThrowsWithExactParameterNames()
    {
        var services = new ServiceCollection();
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(
            () => ServiceExtensions.AddSecurityAuthority(null!, new ComponentKey<ISecurityAuthority>("security.primary")), "services");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(
            () => services.AddSecurityAuthority(default), "authorityKey");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddSecurityAuthority_WhenGivenOnlyAKey_ResolvesTheCollectionsOwnAuthoritySingleton()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.lazy-bound");
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddInMemoryApprovalStore();
        _ = services.AddSecurityAuthority(key);
        using var provider = services.BuildServiceProvider();
        var expectedAuthority = provider.GetRequiredService<ISecurityAuthority>();

        var selector = provider.GetRequiredService<ISecurityAuthoritySelector>();
        var result = await selector.SelectAsync(Context(key), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SecurityAuthoritySelected>().Authority.ShouldBeSameAs(expectedAuthority);
    }

    [Fact]
    public void AddAllowAllSecurityPolicy_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(services.AddAllowAllSecurityPolicy);
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAllowAllSecurityPolicy_WhenCalledTwice_RegistersOneInstance()
    {
        var services = new ServiceCollection();
        _ = services.AddAllowAllSecurityPolicy().AddAllowAllSecurityPolicy();

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(ISecurityPolicy)
                && descriptor.ImplementationType == typeof(AllowAllSecurityPolicy))
            .ShouldBe(1);
    }

    [Fact]
    public void AddAllowAllSecurityPolicy_WhenAppliedAlongsideAgentPermissions_IsResolvedAsAnAdditivePolicy()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddAllowAllSecurityPolicy();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISecurityPolicy>().ShouldContain(policy => policy is AllowAllSecurityPolicy);
    }

    [Fact]
    public void AddStandaloneSecurityProfile_WhenArgumentsAreInvalid_ThrowsWithExactParameterNames()
    {
        var services = new ServiceCollection();
        var profileKey = new SecurityProfileKey("security.standalone");
        var authorityKey = new ComponentKey<ISecurityAuthority>("authority.standalone");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(
            () => ServiceExtensions.AddStandaloneSecurityProfile(
                null!, AgentId(), DefinitionRevision(), ConfigurationVersion(), profileKey, authorityKey),
            "services");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentOutOfRangeException>(
            () => services.AddStandaloneSecurityProfile(
                default, DefinitionRevision(), ConfigurationVersion(), profileKey, authorityKey),
            "agentId");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(
            () => services.AddStandaloneSecurityProfile(
                AgentId(), DefinitionRevision(), ConfigurationVersion(), default, authorityKey),
            "profileKey");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentNullException>(
            () => services.AddStandaloneSecurityProfile(
                AgentId(), DefinitionRevision(), ConfigurationVersion(), profileKey, default),
            "authorityKey");
        AssertExactDefaultSecurityAuthoritySelector<ArgumentOutOfRangeException>(
            () => services.AddStandaloneSecurityProfile(
                AgentId(), DefinitionRevision(), ConfigurationVersion(), profileKey, authorityKey, policyVersion: 0),
            "policyVersion");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddStandaloneSecurityProfile_WhenComposed_CapturesAuthorizationThatItsOwnAuthorityAccepts()
    {
        var agentId = AgentId();
        var revision = DefinitionRevision();
        var configVersion = ConfigurationVersion();
        var profileKey = new SecurityProfileKey("security.standalone");
        var authorityKey = new ComponentKey<ISecurityAuthority>("authority.standalone");
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var scope = new SecurityAuthorizationScope(
            agentId, null, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));

        var services = new ServiceCollection();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddInMemoryApprovalStore();
        _ = services.AddStandaloneSecurityProfile(
            agentId,
            revision,
            configVersion,
            profileKey,
            authorityKey,
            configurePermissions: static options => options.AuditDelivery = SecurityAuditDelivery.BestEffort);
        _ = services.AddAllowAllSecurityPolicy();
        using var provider = services.BuildServiceProvider();

        var profileSelector = provider.GetRequiredService<ISecurityProfileSelector>();
        var captureResult = await profileSelector.SelectAsync(
            new SecurityAuthorizationCaptureRequest(scope, profileKey, revision, configVersion, identity),
            TestContext.Current.CancellationToken);
        var captured = captureResult.ShouldBeOfType<SecurityAuthorizationCaptured>().Authorization;

        var authorityResult = await provider.GetRequiredService<ISecurityAuthoritySelector>()
            .SelectAsync(captured, TestContext.Current.CancellationToken);
        var authority = authorityResult.ShouldBeOfType<SecurityAuthoritySelected>().Authority;

        var request = new SecurityRequest(
            new SecurityRequestId(Guid.NewGuid()),
            scope,
            null,
            identity,
            captured,
            new ComponentId("test.audience"),
            SecurityOperationKind.Process,
            SecurityEffect.Execute,
            [new ProtectedResource(ProtectedResourceKind.Process, "test")],
            new InputFingerprint("sha256:test"),
            DateTimeOffset.UtcNow.AddMinutes(10));

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<SecurityAllowed>();
    }

    [Fact]
    public void AddWorkspaceScopedFileAccessPolicy_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(services.AddWorkspaceScopedFileAccessPolicy);
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddWorkspaceScopedFileAccessPolicy_WhenCalledTwice_RegistersOneInstance()
    {
        var services = new ServiceCollection();
        _ = services.AddWorkspaceScopedFileAccessPolicy().AddWorkspaceScopedFileAccessPolicy();

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(ISecurityPolicy)
                && descriptor.ImplementationType == typeof(WorkspaceScopedFileAccessPolicy))
            .ShouldBe(1);
    }

    [Fact]
    public void AddWorkspaceScopedFileAccessPolicy_WhenAppliedAlongsideAgentPermissions_IsResolvedAsAnAdditivePolicy()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions();
        _ = services.AddWorkspaceScopedFileAccessPolicy();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISecurityPolicy>().ShouldContain(policy => policy is WorkspaceScopedFileAccessPolicy);
    }

    private static SecurityAuthorizationContext Context(ComponentKey<ISecurityAuthority> authorityKey, string fingerprint = "policy-fingerprint") => new(new SecurityProfileKey("security.profile"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d1111111-1111-1111-1111-111111111111")), new SecurityPolicyVersion(1), new ContentHash(fingerprint)), authorityKey, new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(new AgentId(Guid.Parse("d2222222-2222-2222-2222-222222222222")), new SessionId(Guid.Parse("d3333333-3333-3333-3333-333333333333")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("d4444444-4444-4444-4444-444444444444")), new AdmissionId(Guid.Parse("d5555555-5555-5555-5555-555555555555")))), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
    private static void AssertExactDefaultSecurityAuthoritySelector<TException>(Action action, string parameterName)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }

    private static void AssertExactDefaultSecurityAuthoritySelector<TException>(Func<object?> factory, string parameterName)
        where TException : ArgumentException => AssertExactDefaultSecurityAuthoritySelector<TException>(() =>
    {
        _ = factory();
    }, parameterName);
}
