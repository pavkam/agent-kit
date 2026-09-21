// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Tests for <see cref="ContextContributorCompositionValidator"/>.</summary>
public sealed class ContextContributorCompositionValidatorTests
{
    [Fact]
    public void ValidateRegistrations_WhenOrderCollides_ReturnsDiagnostic()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(new ContextContributorDeclaration(
            AgentContextComponentDefaults.AssemblerKey.Value,
            new ContextContributorRegistration(new ContextSourceKey("a"), 5, ContextEvaluationFrequency.OncePerRun, required: false),
            typeof(ReferenceContributor)));
        _ = services.AddSingleton(new ContextContributorDeclaration(
            AgentContextComponentDefaults.AssemblerKey.Value,
            new ContextContributorRegistration(new ContextSourceKey("b"), 5, ContextEvaluationFrequency.OncePerRun, required: false),
            typeof(OtherContributor)));

        var diagnostics = ContextContributorCompositionValidator.ValidateRegistrations(services);

        diagnostics.Length.ShouldBe(1);
        diagnostics[0].Code.ShouldBe("agentkit.context.contributor.order-collision");
    }

    private sealed class ReferenceContributor: IContextContributor
    {
        public ValueTask<ContextContribution> ContributeAsync(ContextContributionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContextContribution([], []));
    }

    private sealed class OtherContributor: IContextContributor
    {
        public ValueTask<ContextContribution> ContributeAsync(ContextContributionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContextContribution([], []));
    }
}
