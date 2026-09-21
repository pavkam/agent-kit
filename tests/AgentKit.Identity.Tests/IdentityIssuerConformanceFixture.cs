// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Composes the first-party <see cref="TestIssuer"/> for reusable issuer contract cases.</summary>
public sealed class IdentityIssuerConformanceFixture: IIdentityIssuerConformanceFixture
{
    private TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = ConformanceCapabilities.All;

    /// <inheritdoc/>
    public DateTimeOffset Now { get; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public async ValueTask<IIdentityIssuer> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var provider = BuildProvider(IdentityIssuerConformanceScenario.Valid);
        return provider.GetRequiredKeyedService<TestIssuer>(new IdentityIssuerId("issuer"));
    }

    /// <inheritdoc/>
    public async ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default) =>
        await _entered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IdentityValidationResult> ValidateEvidenceAsync(
        IdentityIssuerConformanceScenario scenario,
        AuthenticationEvidence evidence,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var provider = BuildProvider(scenario);
        if (scenario == IdentityIssuerConformanceScenario.BlockingValidation)
        {
            var issuer = provider.GetRequiredKeyedService<GatedEvidenceIssuer>(new IdentityIssuerId("issuer"));
            return await issuer.ValidateEvidenceAsync(evidence, evaluatedAt, cancellationToken).ConfigureAwait(false);
        }

        var testIssuer = provider.GetRequiredKeyedService<TestIssuer>(new IdentityIssuerId("issuer"));
        return await testIssuer.ValidateEvidenceAsync(evidence, evaluatedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ServiceProvider BuildProvider(IdentityIssuerConformanceScenario scenario)
    {
        _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        if (scenario == IdentityIssuerConformanceScenario.BlockingValidation)
        {
            var gate = new AsyncGate();
            _entered = gate.Entered;
            _ = services.AddSingleton(gate);
            _ = services.AddIdentityIssuer<GatedEvidenceIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        }
        else
        {
            _ = services.AddSingleton(new TestIssuerSettings(
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                Revoked: scenario == IdentityIssuerConformanceScenario.Revoked));
            _ = services.AddIdentityIssuer<TestIssuer>(new IdentityIssuerRegistration(new IdentityIssuerId("issuer")));
        }

        return services.BuildServiceProvider();
    }
}
