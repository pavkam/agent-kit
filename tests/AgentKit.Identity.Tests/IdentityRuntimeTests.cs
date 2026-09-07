// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

public sealed class IdentityRuntimeTests
{
    [Fact]
    public async Task ResolveAsync_WhenIssuerIsMissing_ReturnsUnknownIssuer()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("missing"), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<IdentityRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.UnknownIssuer);
    }

    [Fact]
    public async Task ResolveAsync_WhenEvidenceIsExpired_ReturnsExpired()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddHours(-2), clock.GetUtcNow().AddHours(-1)));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    [Fact]
    public async Task ResolveAsync_WhenExpiryEqualsClockSkewBoundary_ReturnsExpired()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-3), clock.GetUtcNow().AddMinutes(-2)));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ResolveAsync_WhenZeroSkewExpiryIsAtOrJustBeforeNow_ReturnsExpired(long ticksFromNow)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity(options => options.MaximumClockSkew = TimeSpan.Zero);
        _ = services.RemoveAll<TimeProvider>();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton(new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddTicks(ticksFromNow)));
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    [Fact]
    public async Task ResolveAsync_WhenUnboundedEvidenceEqualsMaximumLifetime_ReturnsExpired()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddHours(-12), null));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    [Fact]
    public async Task ResolveAsync_WhenIssuerRevokesEvidence_ReturnsRevoked()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1), Revoked: true));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Revoked);
    }

    [Fact]
    public async Task ResolveAsync_WhenValid_UsesIssuerVersionAndProvenancedClaims()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1), Version: 7));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        var identity = result.ShouldBeOfType<IdentityResolved>().Identity;
        identity.Version.ShouldBe(new IdentityVersion(7));
        identity.Claims.Single().Issuer.ShouldBe(new IdentityIssuerId("issuer"));
    }

    [Fact]
    public async Task ResolveAsync_WhenIssuerDescriptorChangesDuringNormalization_RetainsInitiallyCapturedVersion()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.RemoveAll<TimeProvider>();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddIdentityIssuer<MutableDescriptorIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("mutable")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("mutable"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityResolved>().Identity.Version.ShouldBe(new IdentityVersion(7));
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
    public async Task ResolveAsync_WhenEvidenceUsesDateTimeOffsetExtremes_ReturnsTypedResultWithoutOverflow()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.MaxValue);
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity(options =>
        {
            options.MaximumClockSkew = TimeSpan.MaxValue;
            options.MaximumEvidenceLifetime = TimeSpan.MaxValue;
        });
        _ = services.RemoveAll<TimeProvider>();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton(new TestIssuerSettings(DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        (result is IdentityResolved or IdentityRejected).ShouldBeTrue();
    }

    [Fact]
    public void AddAgentIdentity_WhenCalledTwice_KeepsSingularRuntimeServices()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddAgentIdentity();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IExecutionIdentityResolver>().Count().ShouldBe(1);
        provider.GetServices<IDelegatedIdentityDeriver>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task ResolveAsync_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledException()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), cancellation.Token));
    }

    [Fact]
    public void AddIdentityIssuer_WhenKeyIsDuplicated_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));

        _ = Should.Throw<InvalidOperationException>(() => services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer"))));
    }

    [Fact]
    public void IdentityIssuerRegistration_WhenIssuerIsDefault_ThrowsWithIssuerIdParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityIssuerRegistration(default));

        exception.ParamName.ShouldBe("issuerId");
    }

    [Fact]
    public void AddIdentityNormalizationPolicy_WhenNameIsDuplicated_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddIdentityNormalizationPolicy<NullPolicy>(new IdentityNormalizationPolicyRegistration("duplicate"));

        _ = Should.Throw<InvalidOperationException>(() => services.AddIdentityNormalizationPolicy<NullPolicy>(new IdentityNormalizationPolicyRegistration("duplicate")));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, -1)]
    public void AddAgentIdentity_WhenOptionsAreInvalid_FailsValidationOnResolution(int depth, int lifetimeHours, int skewMinutes)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity(options =>
        {
            options.MaximumDelegationDepth = depth;
            options.MaximumEvidenceLifetime = TimeSpan.FromHours(lifetimeHours);
            options.MaximumClockSkew = TimeSpan.FromMinutes(skewMinutes);
        });

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IDelegatedIdentityDeriver>);
    }

    [Theory]
    [InlineData(0, "issuers")]
    [InlineData(1, "normalization")]
    [InlineData(2, "validation")]
    [InlineData(3, "logger")]
    public void ExecutionIdentityResolver_WhenDependencyIsNull_ThrowsWithParameterName(int dependency, string parameterName)
    {
        var issuers = new IdentityIssuerCatalog([]);
        var normalization = new OrderedIdentityNormalizationPolicies([]);
        var validation = new NullValidationPolicy();
        var logger = NullLogger<ExecutionIdentityResolver>.Instance;

        var exception = Should.Throw<ArgumentNullException>(() => _ = dependency switch
        {
            0 => new ExecutionIdentityResolver(null!, normalization, validation, logger),
            1 => new ExecutionIdentityResolver(issuers, null!, validation, logger),
            2 => new ExecutionIdentityResolver(issuers, normalization, null!, logger),
            _ => new ExecutionIdentityResolver(issuers, normalization, validation, null!),
        });

        exception.ParamName.ShouldBe(parameterName);
    }

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

    [Theory]
    [InlineData(0, "issuers")]
    [InlineData(1, "timeProvider")]
    [InlineData(2, "options")]
    public void DefaultIdentityValidationPolicy_WhenDependencyIsNull_ThrowsWithParameterName(int dependency, string parameterName)
    {
        var issuers = new IdentityIssuerCatalog([]);
        var clock = new FakeTimeProvider();
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.Zero, TimeSpan.FromHours(1));

        var exception = Should.Throw<ArgumentNullException>(() => _ = dependency switch
        {
            0 => new DefaultIdentityValidationPolicy(null!, clock, options),
            1 => new DefaultIdentityValidationPolicy(issuers, null!, options),
            _ => new DefaultIdentityValidationPolicy(issuers, clock, null!),
        });

        exception.ParamName.ShouldBe(parameterName);
    }

    [Theory]
    [InlineData(0, 0, 1, "maximumDelegationDepth")]
    [InlineData(1, -1, 1, "maximumClockSkew")]
    [InlineData(1, 0, 0, "maximumEvidenceLifetime")]
    public void AgentIdentityOptionsSnapshot_WhenConstraintIsInvalid_ThrowsWithParameterName(int depth, long skewTicks, long lifetimeTicks, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentIdentityOptionsSnapshot(false, depth, TimeSpan.FromTicks(skewTicks), TimeSpan.FromTicks(lifetimeTicks)));

        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void RuntimeBindings_WhenDependencyIsNull_ThrowWithParameterName()
    {
        var registration = new IdentityIssuerRegistration(new IdentityIssuerId("issuer"));
        var policyRegistration = new IdentityNormalizationPolicyRegistration("policy");
        var issuer = new TestIssuer(new TestIssuerSettings(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), new IdentityIssuerId("issuer"));
        var policy = new NullPolicy();

        Should.Throw<ArgumentNullException>(() => new IdentityIssuerBinding(null!, registration)).ParamName.ShouldBe("issuer");
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerBinding(issuer, null!)).ParamName.ShouldBe("registration");
        Should.Throw<ArgumentNullException>(() => new IdentityNormalizationPolicyBinding(null!, policyRegistration)).ParamName.ShouldBe("policy");
        Should.Throw<ArgumentNullException>(() => new IdentityNormalizationPolicyBinding(policy, null!)).ParamName.ShouldBe("registration");
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerCatalog(null!)).ParamName.ShouldBe("bindings");
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerCatalog([null!])).ParamName.ShouldBe("binding");
        _ = Should.Throw<ArgumentException>(() => new IdentityIssuerCatalog([new IdentityIssuerBinding(issuer, registration), new IdentityIssuerBinding(issuer, registration)]));
        Should.Throw<ArgumentNullException>(() => new OrderedIdentityNormalizationPolicies(null!)).ParamName.ShouldBe("bindings");
        Should.Throw<ArgumentNullException>(() => new OrderedIdentityNormalizationPolicies([null!])).ParamName.ShouldBe("binding");
        Should.Throw<ArgumentException>(() => new IdentityIssuerCatalog([]).Find(default)).ParamName.ShouldBe("issuerId");
    }

    [Fact]
    public async Task ResolveAsync_WhenPoliciesAreRegisteredInReverseOrder_AppliesConfiguredOrderThenName()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var trace = new PolicyTrace();
        var services = new ServiceCollection();
        _ = services.AddSingleton(trace);
        _ = services.AddSingleton(new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddAgentIdentity();
        _ = services.RemoveAll<TimeProvider>();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        _ = services.AddIdentityNormalizationPolicy<ZuluPolicy>(new IdentityNormalizationPolicyRegistration("zulu", 10));
        _ = services.AddIdentityNormalizationPolicy<AlphaPolicy>(new IdentityNormalizationPolicyRegistration("alpha", 10));
        _ = services.AddIdentityNormalizationPolicy<FirstPolicy>(new IdentityNormalizationPolicyRegistration("first", 1));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        _ = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        trace.Entries.ShouldBe(["first", "alpha", "zulu"]);
    }

    [Fact]
    public async Task ResolveAsync_WhenIssuerReturnsNullNormalizationResult_RejectsUnavailable()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1), NullNormalization: true));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
    }

    [Fact]
    public async Task ResolveAsync_WhenPolicyReturnsNullNormalizationResult_RejectsUnavailable()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddIdentityNormalizationPolicy<NullPolicy>(new IdentityNormalizationPolicyRegistration("null"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
    }

    [Fact]
    public async Task ResolveAsync_WhenSameIssuerClassUsesTwoKeys_ResolvesEachKeyedInstance()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("second")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("second"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityResolved>().Identity.Evidence.Issuer.ShouldBe(new IdentityIssuerId("second"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResolveAsync_WhenPolicyReplacesCapturedEvidenceOrVersion_RejectsUnavailable(bool replaceEvidence)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = replaceEvidence
            ? services.AddIdentityNormalizationPolicy<ReplacingEvidencePolicy>(new IdentityNormalizationPolicyRegistration("replace-evidence"))
            : services.AddIdentityNormalizationPolicy<ReplacingVersionPolicy>(new IdentityNormalizationPolicyRegistration("replace-version"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
    }

    [Fact]
    public async Task ResolveAsync_WhenValidatorReturnsNull_RejectsUnavailable()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.ReplaceIdentityValidationPolicy<NullValidationPolicy>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
    }

    [Fact]
    public async Task ResolveAsync_WhenIssuerWaitIsCancelled_PropagatesCancellation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddIdentityIssuer<GatedIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("gated")));
        _ = services.AddSingleton<ILogger<ExecutionIdentityResolver>, ThrowingLogger<ExecutionIdentityResolver>>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var issuer = provider.GetRequiredKeyedService<GatedIssuer>(new IdentityIssuerId("gated"));
        using var cancellation = new CancellationTokenSource();
        using var listener = ThrowingStoppedListener();

        var pending = scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("gated"), cancellation.Token).AsTask();
        await issuer.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResolveAsync_WhenPolicyWaitIsCancelled_PropagatesCancellation(bool normalization)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var gate = new AsyncGate();
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddSingleton(gate);
        _ = normalization
            ? services.AddIdentityNormalizationPolicy<GatedNormalizationPolicy>(new IdentityNormalizationPolicyRegistration("gated"))
            : services.ReplaceIdentityValidationPolicy<GatedValidationPolicy>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();

        var pending = scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), cancellation.Token).AsTask();
        await gate.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
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
    public async Task ResolveAsync_WhenRejectedAndDiagnosticsThrow_PreservesTypedRejection()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentIdentity();
        _ = services.AddSingleton<ILogger<ExecutionIdentityResolver>, ThrowingLogger<ExecutionIdentityResolver>>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var listener = ThrowingStoppedListener();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.UnknownIssuer);
    }

    [Fact]
    public async Task ResolveAsync_WhenDiagnosticsThrow_StillReturnsResolvedIdentity()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddSingleton<ILogger<ExecutionIdentityResolver>, ThrowingLogger<ExecutionIdentityResolver>>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = static _ => throw new InvalidOperationException("hostile stopped callback"),
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = static (_, _) => throw new InvalidOperationException("hostile instrument listener");
        try
        {
            meterListener.Start();
        }
        catch (InvalidOperationException)
        {
            // Existing instruments are synchronously published by Start; the operation must remain independent.
        }

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<IdentityResolved>();
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
    public async Task ResolveAsync_WhenAmbientActivityExists_PreservesParentSpan()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        using var scope = provider.CreateScope();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static _ => true,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var parentSource = new ActivitySource("identity-parent-test");
        using var parent = parentSource.StartActivity("parent")!;
        Activity? observed = null;
        listener.ActivityStarted = activity => { if (activity.OperationName == AgentKitActivityNames.IdentityResolve) { observed = activity; } };

        _ = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        observed!.ParentSpanId.ShouldBe(parent.SpanId);
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

    private static IdentityAssertion Assertion(string issuer) => new(new IdentityIssuerId(issuer), "external-subject", [], Evidence(issuer, new DateTimeOffset(2026, 9, 7, 11, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 7, 13, 0, 0, TimeSpan.Zero)));

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
