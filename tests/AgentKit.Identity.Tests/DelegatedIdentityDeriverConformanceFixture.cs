// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Composes the first-party delegated-identity deriver for reusable contract cases.</summary>
public sealed class DelegatedIdentityDeriverConformanceFixture: IDelegatedIdentityDeriverConformanceFixture
{
    private TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = ConformanceCapabilities.All;

    /// <inheritdoc/>
    public DateTimeOffset Now { get; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public int MaximumDelegationDepth { get; } = 1;

    /// <inheritdoc/>
    public async ValueTask<IDelegatedIdentityDeriver> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var provider = BuildProvider(DelegatedIdentityDeriverConformanceScenario.ValidNarrow);
        return provider.GetRequiredService<IDelegatedIdentityDeriver>();
    }

    /// <inheritdoc/>
    public async ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default) =>
        await _entered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public ExecutionIdentity CreateParentIdentity()
    {
        var evidence = Evidence("issuer", Now.AddMinutes(-5), Now.AddHours(1));
        return new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            evidence,
            [Claim("issuer", "scope", "read"), Claim("issuer", "role", "writer")],
            [],
            IdentityAssuranceLevel.Strong,
            new IdentityVersion(1));
    }

    /// <inheritdoc/>
    public async ValueTask<IdentityResolutionResult> DeriveAsync(
        DelegatedIdentityDeriverConformanceScenario scenario,
        DelegatedIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (scenario == DelegatedIdentityDeriverConformanceScenario.ExpiredParent)
        {
            var expired = CreateExpiredParentIdentity();
            request = new DelegatedIdentityRequest(
                request.DelegationId,
                expired,
                expired.Claims,
                expired.Assurance);
        }

        await using var provider = BuildProvider(scenario);
        return await provider.GetRequiredService<IDelegatedIdentityDeriver>()
            .DeriveAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ExecutionIdentity CreateExpiredParentIdentity()
    {
        var evidence = Evidence("issuer", Now.AddHours(-2), Now.AddHours(-1));
        return new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            evidence,
            [Claim("issuer", "scope", "read")],
            [],
            IdentityAssuranceLevel.Basic,
            new IdentityVersion(1));
    }

    private ServiceProvider BuildProvider(DelegatedIdentityDeriverConformanceScenario scenario)
    {
        _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        _ = services.AddSingleton(new TestIssuerSettings(Now.AddMinutes(-5), Now.AddHours(1)));
        _ = services.AddAgentIdentity(options =>
        {
            options.MaximumDelegationDepth = scenario == DelegatedIdentityDeriverConformanceScenario.MaximumDepth
                ? MaximumDelegationDepth
                : 4;
        });
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        if (scenario == DelegatedIdentityDeriverConformanceScenario.BlockingDerivation)
        {
            var gate = new AsyncGate();
            _entered = gate.Entered;
            _ = services.AddSingleton(gate);
            _ = services.ReplaceIdentityValidationPolicy<GatedValidationPolicy>();
        }

        return services.BuildServiceProvider();
    }
}
