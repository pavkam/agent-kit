// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies ExecutionIdentityResolver behavior and contracts.</summary>
public sealed class ExecutionIdentityResolverTests: IdentityNormalizerConformanceTests<IdentityNormalizerConformanceFixture>
{
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

    /// <inheritdoc/>
    protected override IdentityNormalizerConformanceFixture CreateFixture() => new();

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
    public async Task ResolveAsync_WhenIssuerExplicitlyRejectsNormalization_PreservesItsTypedFailure()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        using var provider = BuildProvider(
            clock,
            new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1), RejectNormalization: true));
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(
            Assertion("issuer"), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<IdentityRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.Malformed);
        rejected.Failure.SafeMessage.ShouldBe("rejected by issuer");
    }

    [Fact]
    public async Task ResolveAsync_WhenNormalizationPolicyExplicitlyRejects_PreservesItsTypedFailure()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.AddIdentityNormalizationPolicy<RejectingPolicy>(new IdentityNormalizationPolicyRegistration("rejecting"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(
            Assertion("issuer"), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<IdentityRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.Malformed);
        rejected.Failure.SafeMessage.ShouldBe("rejected by policy");
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
        _ = replaceEvidence ? services.AddIdentityNormalizationPolicy<ReplacingEvidencePolicy>(new IdentityNormalizationPolicyRegistration("replace-evidence")) : services.AddIdentityNormalizationPolicy<ReplacingVersionPolicy>(new IdentityNormalizationPolicyRegistration("replace-version"));
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
    public async Task ResolveAsync_WhenValidatorThrowsUnexpectedly_LogsResolveFailedAndRejectsUnavailable()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var services = CreateServices(clock, new TestIssuerSettings(clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(1)));
        _ = services.ReplaceIdentityValidationPolicy<ThrowingValidationPolicy>();
        var logger = new RecordingLogger<ExecutionIdentityResolver>();
        _ = services.AddSingleton<ILogger<ExecutionIdentityResolver>>(logger);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Unavailable);
        var failed = logger.Entries.Single(entry => entry.EventId == 17003);
        failed.Level.ShouldBe(LogLevel.Error);
        failed.Message.ShouldContain(nameof(InvalidOperationException));
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
        _ = normalization ? services.AddIdentityNormalizationPolicy<GatedNormalizationPolicy>(new IdentityNormalizationPolicyRegistration("gated")) : services.ReplaceIdentityValidationPolicy<GatedValidationPolicy>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var pending = scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), cancellation.Token).AsTask();
        await gate.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
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
        listener.ActivityStarted = activity =>
        {
            if (activity.OperationName == AgentKitActivityNames.IdentityResolve)
            {
                observed = activity;
            }
        };
        _ = await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(Assertion("issuer"), TestContext.Current.CancellationToken);
        observed!.ParentSpanId.ShouldBe(parent.SpanId);
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
