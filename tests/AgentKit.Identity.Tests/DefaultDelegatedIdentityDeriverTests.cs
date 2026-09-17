// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies DefaultDelegatedIdentityDeriver behavior and contracts.</summary>
public sealed class DefaultDelegatedIdentityDeriverTests
{
    [Theory]
    [InlineData(0, "timeProvider")]
    [InlineData(1, "options")]
    [InlineData(2, "validation")]
    [InlineData(3, "logger")]
    public void DefaultDelegatedIdentityDeriver_WhenDependencyIsNull_ThrowsWithParameterName(int dependency, string parameterName)
    {
        var clock = new FakeTimeProvider();
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.Zero, TimeSpan.FromHours(1));
        var validation = new NullValidationPolicy();
        var logger = NullLogger<DefaultDelegatedIdentityDeriver>.Instance;
        var exception = Should.Throw<ArgumentNullException>(() => _ = dependency switch
        {
            0 => new DefaultDelegatedIdentityDeriver(null!, options, validation, logger),
            1 => new DefaultDelegatedIdentityDeriver(clock, null!, validation, logger),
            2 => new DefaultDelegatedIdentityDeriver(clock, options, null!, logger),
            _ => new DefaultDelegatedIdentityDeriver(clock, options, validation, null!),
        });
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public async Task DeriveAsync_WhenClaimsAndAssuranceNarrow_RetainsTenantPrincipalAndChain()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "role", "admin"), Claim("issuer", "scope", "read")], IdentityAssuranceLevel.Strong);
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, [parent.Claims[1]], IdentityAssuranceLevel.Basic), TestContext.Current.CancellationToken);
        var child = result.ShouldBeOfType<IdentityResolved>().Identity;
        child.TenantId.ShouldBe(parent.TenantId);
        child.PrincipalId.ShouldBe(parent.PrincipalId);
        child.Claims.ShouldBe([parent.Claims[1]]);
        child.DelegationChain.Length.ShouldBe(1);
    }

    [Fact]
    public async Task DeriveAsync_WhenDelegationIdRepeats_ReturnsTypedRejection()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var delegationId = new DelegationId(Guid.NewGuid());
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        var deriver = provider.GetRequiredService<IDelegatedIdentityDeriver>();
        var first = await deriver.DeriveAsync(new DelegatedIdentityRequest(delegationId, parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        var result = await deriver.DeriveAsync(new DelegatedIdentityRequest(delegationId, first.ShouldBeOfType<IdentityResolved>().Identity, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    [Fact]
    public async Task DeriveAsync_WhenParentAncestryAlreadyRepeatsADelegation_ReturnsTypedRejection()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var id = new DelegationId(Guid.NewGuid());
        var claim = Claim("issuer", "scope", "read");
        var evidence = Evidence("issuer", clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1));
        var link = new DelegationIdentityLink(id, new TenantId("tenant"), new PrincipalId("principal"), evidence.Issuer, evidence.Id, new IdentityVersion(1), clock.GetUtcNow(), [claim], IdentityAssuranceLevel.Basic);
        var parent = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, [claim], [link, link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    [Fact]
    public async Task DeriveAsync_WhenParentIsAtMaximumDepth_ReturnsTypedRejection()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var claim = Claim("issuer", "scope", "read");
        var evidence = Evidence("issuer", clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1));
        var link = new DelegationIdentityLink(new DelegationId(Guid.NewGuid()), new TenantId("tenant"), new PrincipalId("principal"), evidence.Issuer, evidence.Id, new IdentityVersion(1), clock.GetUtcNow(), [claim], IdentityAssuranceLevel.Basic);
        var parent = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.Configure<AgentIdentityOptions>(options => options.MaximumDelegationDepth = 1);
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    [Fact]
    public async Task DeriveAsync_WhenRetainedHistoryBroadensClaims_ReturnsTypedRejection()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var retained = Claim("issuer", "scope", "read");
        var added = Claim("issuer", "role", "admin");
        var evidence = Evidence("issuer", clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1));
        var first = new DelegationIdentityLink(new DelegationId(Guid.NewGuid()), new TenantId("tenant"), new PrincipalId("principal"), evidence.Issuer, evidence.Id, new IdentityVersion(1), clock.GetUtcNow(), [retained], IdentityAssuranceLevel.Basic);
        var second = new DelegationIdentityLink(new DelegationId(Guid.NewGuid()), new TenantId("tenant"), new PrincipalId("principal"), evidence.Issuer, evidence.Id, new IdentityVersion(1), clock.GetUtcNow(), [retained, added], IdentityAssuranceLevel.Basic);
        var parent = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, second.Claims, [first, second], second.Assurance, new IdentityVersion(1));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    [Fact]
    public async Task DeriveAsync_WhenParentTerminalClaimsBroadenLatestRetainedLink_ReturnsTypedRejection()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var retained = Claim("issuer", "scope", "read");
        var added = Claim("issuer", "role", "admin");
        var evidence = Evidence("issuer", clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1));
        var link = new DelegationIdentityLink(new DelegationId(Guid.NewGuid()), new TenantId("tenant"), new PrincipalId("principal"), evidence.Issuer, evidence.Id, new IdentityVersion(1), clock.GetUtcNow(), [retained], IdentityAssuranceLevel.Strong);
        var parent = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, [retained, added], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    [Fact]
    public async Task DeriveAsync_WhenParentEvidenceIsRevoked_RejectsBeforeCreatingChild()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1), Revoked: true));
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Revoked);
    }

    [Fact]
    public async Task DeriveAsync_WhenParentEvidenceIsExpired_RejectsBeforeCreatingChild()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer", clock.GetUtcNow().AddHours(-2), clock.GetUtcNow().AddHours(-1)), [Claim("issuer", "scope", "read")], [], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    [Fact]
    public async Task DeriveAsync_WhenValidationWaitIsCancelled_PropagatesCancellation()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        var gate = new AsyncGate();
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddSingleton(gate);
        _ = services.ReplaceIdentityValidationPolicy<GatedValidationPolicy>();
        _ = services.AddSingleton<ILogger<DefaultDelegatedIdentityDeriver>, ThrowingLogger<DefaultDelegatedIdentityDeriver>>();
        using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        using var listener = ThrowingStoppedListener();
        var pending = provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), cancellation.Token).AsTask();
        await gate.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
    }

    [Fact]
    public async Task DeriveAsync_WhenValidationWaitIsCancelled_LogsDeriveCancelled()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        var gate = new AsyncGate();
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddSingleton(gate);
        _ = services.ReplaceIdentityValidationPolicy<GatedValidationPolicy>();
        var logger = new RecordingLogger<DefaultDelegatedIdentityDeriver>();
        _ = services.AddSingleton<ILogger<DefaultDelegatedIdentityDeriver>>(logger);
        using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var pending = provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), cancellation.Token).AsTask();
        await gate.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);

        var cancelled = logger.Entries.Single(entry => entry.EventId == 17005);
        cancelled.Level.ShouldBe(LogLevel.Debug);
    }

    [Fact]
    public async Task DeriveAsync_WhenValidatorThrows_ReturnsUnavailableAndCompletesDiagnostics()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.ReplaceIdentityValidationPolicy<ThrowingValidationPolicy>();
        _ = services.AddSingleton<ILogger<DefaultDelegatedIdentityDeriver>, ThrowingLogger<DefaultDelegatedIdentityDeriver>>();
        using var provider = services.BuildServiceProvider();
        using var listener = ThrowingStoppedListener();
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
    }

    [Fact]
    public async Task DeriveAsync_WhenValidatorThrowsUnexpectedly_LogsDeriveFailedAndRejectsUnavailable()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.ReplaceIdentityValidationPolicy<ThrowingValidationPolicy>();
        var logger = new RecordingLogger<DefaultDelegatedIdentityDeriver>();
        _ = services.AddSingleton<ILogger<DefaultDelegatedIdentityDeriver>>(logger);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
        var failed = logger.Entries.Single(entry => entry.EventId == 17004);
        failed.Level.ShouldBe(LogLevel.Error);
        failed.Message.ShouldContain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task DeriveAsync_WhenSamplerAndLoggerThrow_StillReturnsDerivedIdentity()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddSingleton<ILogger<DefaultDelegatedIdentityDeriver>, ThrowingLogger<DefaultDelegatedIdentityDeriver>>();
        using var provider = services.BuildServiceProvider();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => throw new InvalidOperationException("hostile sampler"),
        };
        ActivitySource.AddActivityListener(listener);
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<IdentityResolved>();
    }

    [Fact]
    public async Task DeriveAsync_WhenValidatorReturnsNull_RejectsUnavailableWithoutChild()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var parent = Identity(clock, [Claim("issuer", "scope", "read")]);
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.ReplaceIdentityValidationPolicy<NullValidationPolicy>();
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IDelegatedIdentityDeriver>().DeriveAsync(new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, parent.Claims, parent.Assurance), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
    }

    private static ServiceProvider BuildProvider(FakeTimeProvider? clock = null, TestIssuerSettings? issuer = null)
    {
        var services = CreateServices(clock ?? new FakeTimeProvider(), issuer);
        return services.BuildServiceProvider();
    }

    private static ServiceCollection CreateServices(FakeTimeProvider clock, TestIssuerSettings? issuer)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.RemoveAll<TimeProvider>();
        _ = services.AddSingleton<TimeProvider>(clock);
        if (issuer is not null)
        {
            _ = services.AddSingleton(issuer);
            _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        }

        return services;
    }

    private static AuthenticationEvidence Evidence(string issuer, DateTimeOffset authenticatedAt, DateTimeOffset? expiresAt) => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "test", authenticatedAt, expiresAt, new AuthenticationEvidenceFingerprint(new ContentHash("safe")));
    private static IdentityClaim Claim(string issuer, string type, string value) => new(new IdentityIssuerId(issuer), type, value, IdentityClaimValueKind.Text);
    private static ExecutionIdentity Identity(FakeTimeProvider clock, ImmutableArray<IdentityClaim> claims, IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Basic) => new(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer", clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)), claims, [], assurance, new IdentityVersion(1));
    private static ActivityListener ThrowingStoppedListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = static _ => throw new InvalidOperationException("hostile stopped callback"),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
