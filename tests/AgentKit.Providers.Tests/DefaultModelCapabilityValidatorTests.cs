// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit;
using AgentKit.Providers;

/// <summary>
/// Exercises capability validation, including which behaviors may be
/// downgraded and which must fail closed.
/// </summary>
public sealed class DefaultModelCapabilityValidatorTests
{
    private readonly IModelCapabilityValidator _validator = CreateValidator();

    [Fact]
    public async Task ValidateAsync_WhenNoRequirements_ReturnsSupported()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m"),
            ModelRequirements.None,
            CapabilityDowngradePolicy.Reject,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CapabilitiesSupported>();
    }

    [Fact]
    public async Task ValidateAsync_WhenToolCallsRequiredAndMissing_ReturnsUnsupported()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", toolCalls: false),
            new ModelRequirements { RequiresToolCalls = true },
            CapabilityDowngradePolicy.Reject,
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CapabilitiesUnsupported>();
        unsupported.Capabilities.ShouldHaveSingleItem()
            .Capability.ShouldBe(ModelCapabilityKind.ToolCalls);
    }

    [Fact]
    public async Task ValidateAsync_WhenToolCallsMissingAndDowngradeAllowed_StillUnsupported()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", toolCalls: false),
            new ModelRequirements { RequiresToolCalls = true },
            CapabilityDowngradePolicy.AllowDeclaredAdjustments,
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CapabilitiesUnsupported>();
        unsupported.Capabilities.ShouldHaveSingleItem()
            .Capability.ShouldBe(ModelCapabilityKind.ToolCalls);
    }

    [Fact]
    public async Task ValidateAsync_WhenStreamingMissingAndDowngradeRejected_ReturnsUnsupported()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", streaming: false),
            new ModelRequirements { RequiresStreaming = true },
            CapabilityDowngradePolicy.Reject,
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CapabilitiesUnsupported>();
        unsupported.Capabilities.ShouldHaveSingleItem()
            .Capability.ShouldBe(ModelCapabilityKind.Streaming);
    }

    [Fact]
    public async Task ValidateAsync_WhenStreamingMissingAndDowngradeAllowed_ReturnsDeclaredAdjustment()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", streaming: false),
            new ModelRequirements { RequiresStreaming = true },
            CapabilityDowngradePolicy.AllowDeclaredAdjustments,
            TestContext.Current.CancellationToken);

        var downgraded = result.ShouldBeOfType<CapabilitiesDowngraded>();
        var adjustment = downgraded.Adjustments.ShouldHaveSingleItem();
        adjustment.Capability.ShouldBe(ModelCapabilityKind.Streaming);
        adjustment.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidateAsync_WhenParallelToolCallsMissingButToolsSupported_Downgrades()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", toolCalls: true, parallelToolCalls: false),
            new ModelRequirements { RequiresToolCalls = true, RequiresParallelToolCalls = true },
            CapabilityDowngradePolicy.AllowDeclaredAdjustments,
            TestContext.Current.CancellationToken);

        var downgraded = result.ShouldBeOfType<CapabilitiesDowngraded>();
        downgraded.Adjustments.ShouldHaveSingleItem()
            .Capability.ShouldBe(ModelCapabilityKind.ParallelToolCalls);
    }

    [Fact]
    public async Task ValidateAsync_WhenParallelToolCallsMissingAndToolsUnsupported_DoesNotDowngrade()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", toolCalls: false, parallelToolCalls: false),
            new ModelRequirements { RequiresToolCalls = true, RequiresParallelToolCalls = true },
            CapabilityDowngradePolicy.AllowDeclaredAdjustments,
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CapabilitiesUnsupported>();
        unsupported.Capabilities.Select(capability => capability.Capability)
            .ShouldBe([ModelCapabilityKind.ToolCalls, ModelCapabilityKind.ParallelToolCalls]);
    }

    [Fact]
    public async Task ValidateAsync_WhenContextWindowTooSmall_ReturnsUnsupported()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", maxContextTokens: 1_000),
            new ModelRequirements { MinimumInputTokens = 4_000 },
            CapabilityDowngradePolicy.AllowDeclaredAdjustments,
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CapabilitiesUnsupported>();
        unsupported.Capabilities.ShouldHaveSingleItem()
            .Capability.ShouldBe(ModelCapabilityKind.ContextWindow);
    }

    [Fact]
    public async Task ValidateAsync_WhenContextWindowExactlyMeetsMinimum_ReturnsSupported()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", maxContextTokens: 4_000),
            new ModelRequirements { MinimumInputTokens = 4_000 },
            CapabilityDowngradePolicy.Reject,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CapabilitiesSupported>();
    }

    [Fact]
    public async Task ValidateAsync_WhenModelDeclaresNoContextLimit_TreatsMinimumAsUnconstrained()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", maxContextTokens: null),
            new ModelRequirements { MinimumInputTokens = 1_000_000 },
            CapabilityDowngradePolicy.Reject,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CapabilitiesSupported>();
    }

    [Fact]
    public async Task ValidateAsync_WhenSeveralRequirementsMissing_ReportsAllOfThem()
    {
        var result = await _validator.ValidateAsync(
            ProviderTestData.Model("m", systemInstructions: false, structuredOutput: false),
            new ModelRequirements
            {
                RequiresSystemInstructions = true,
                RequiresStructuredOutput = true,
            },
            CapabilityDowngradePolicy.Reject,
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CapabilitiesUnsupported>();
        unsupported.Capabilities.Length.ShouldBe(2);
    }

    [Fact]
    public async Task ValidateAsync_WhenModelIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await _validator.ValidateAsync(
                null!,
                ModelRequirements.None,
                CapabilityDowngradePolicy.Reject,
                TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public async Task ValidateAsync_WhenDowngradePolicyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await _validator.ValidateAsync(
                ProviderTestData.Model("m"),
                ModelRequirements.None,
                (CapabilityDowngradePolicy) 42,
                TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("downgradePolicy");
    }

    [Fact]
    public async Task ValidateAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await _validator.ValidateAsync(
                ProviderTestData.Model("m"),
                ModelRequirements.None,
                CapabilityDowngradePolicy.Reject,
                cancellation.Token));
    }

    private static IModelCapabilityValidator CreateValidator()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentProviders();
        return services.BuildServiceProvider().GetRequiredService<IModelCapabilityValidator>();
    }
}
