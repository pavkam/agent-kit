// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Composes the first-party identity resolver for reusable normalization contract cases.</summary>
public sealed class IdentityNormalizerConformanceFixture: IIdentityNormalizerConformanceFixture
{
    private TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <inheritdoc/>
    public DateTimeOffset Now { get; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public IdentityVersion ExpectedIssuerVersion { get; } = new(7);

    /// <inheritdoc/>
    public async ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default) => await _entered.Task.WaitAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<IdentityResolutionResult> ResolveAsync(IdentityAssertion assertion, IdentityNormalizerScenario scenario, CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = services.AddSingleton(new ConformanceIssuerSettings(scenario, _entered));
        _ = services.AddAgentIdentity(options => options.MaximumClockSkew = TimeSpan.Zero);
        _ = services.AddIdentityIssuer<ConformanceIssuer>(new IdentityIssuerRegistration(assertion.Issuer));
        if (scenario == IdentityNormalizerScenario.Narrow)
        {
            _ = services.AddIdentityNormalizationPolicy<ConformanceClaimNarrowingPolicy>(new IdentityNormalizationPolicyRegistration("claims", 1));
            _ = services.AddIdentityNormalizationPolicy<ConformanceAssuranceNarrowingPolicy>(new IdentityNormalizationPolicyRegistration("assurance", 2));
        }
        else if (scenario == IdentityNormalizerScenario.RestoreRemovedClaim)
        {
            _ = services.AddIdentityNormalizationPolicy<ConformanceClaimNarrowingPolicy>(new IdentityNormalizationPolicyRegistration("claims", 1));
            _ = services.AddIdentityNormalizationPolicy<ConformanceClaimRestoringPolicy>(new IdentityNormalizationPolicyRegistration("restore", 2));
        }
        else if (scenario is IdentityNormalizerScenario.ReplaceEvidence or IdentityNormalizerScenario.ReplaceVersion or IdentityNormalizerScenario.WidenClaims or IdentityNormalizerScenario.WidenAssurance or IdentityNormalizerScenario.ReplaceTenant or IdentityNormalizerScenario.ReplacePrincipal or IdentityNormalizerScenario.ReplaceSubjectKind or IdentityNormalizerScenario.ReplaceDelegationChain)
        {
            _ = services.AddIdentityNormalizationPolicy<ConformanceNormalizationPolicy>(new IdentityNormalizationPolicyRegistration("conformance"));
        }

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IExecutionIdentityResolver>().ResolveAsync(assertion, cancellationToken);
    }
}
