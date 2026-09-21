// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Composes the first-party validation policy for reusable contract cases.</summary>
public sealed class IdentityValidationPolicyConformanceFixture: IIdentityValidationPolicyConformanceFixture
{
    private TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = ConformanceCapabilities.All;

    /// <inheritdoc/>
    public DateTimeOffset Now { get; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public TimeSpan MaximumClockSkew { get; } = TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan MaximumEvidenceLifetime { get; } = TimeSpan.FromHours(2);

    /// <inheritdoc/>
    public async ValueTask<IIdentityValidationPolicy> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var provider = BuildProvider(IdentityValidationPolicyConformanceScenario.Valid);
        return provider.GetRequiredService<IIdentityValidationPolicy>();
    }

    /// <inheritdoc/>
    public async ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default) =>
        await _entered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IdentityValidationResult> ValidateAsync(
        IdentityValidationPolicyConformanceScenario scenario,
        ExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        await using var provider = BuildProvider(scenario);
        return await provider.GetRequiredService<IIdentityValidationPolicy>()
            .ValidateAsync(identity, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ServiceProvider BuildProvider(IdentityValidationPolicyConformanceScenario scenario)
    {
        _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        _ = services.AddSingleton(new TestIssuerSettings(Now.AddMinutes(-5), Now.AddMinutes(30)));
        _ = services.AddAgentIdentity(options =>
        {
            options.MaximumClockSkew = MaximumClockSkew;
            options.MaximumEvidenceLifetime = MaximumEvidenceLifetime;
        });
        _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        if (scenario == IdentityValidationPolicyConformanceScenario.BlockingValidation)
        {
            var gate = new AsyncGate();
            _entered = gate.Entered;
            _ = services.AddSingleton(gate);
            _ = services.ReplaceIdentityValidationPolicy<GatedValidationPolicy>();
        }

        return services.BuildServiceProvider();
    }
}
