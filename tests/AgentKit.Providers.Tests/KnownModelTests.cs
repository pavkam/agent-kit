// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>Verifies KnownModel behavior and contracts.</summary>
public sealed class KnownModelTests
{
    private static readonly ProviderId _provider = new("openai");
    private static readonly ModelId _model = new("gpt-test");
    private static readonly ModelLimits _limits = new(128_000, 16_384);
    private static readonly ApiFamilyId _apiFamily = new("openai-chat-completions");

    private static readonly ModelCapabilities _baseline = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: true,
        supportsStructuredOutput: true,
        supportsReasoning: false,
        supportsVisionInput: false,
        ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenProviderIdIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KnownModel(
            default, _model, "GPT Test", KnownModelStatus.Available, false, false, true, _limits, null, null));

        exception.ParamName.ShouldBe("providerId");
    }

    [Fact]
    public void Constructor_WhenModelIdIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KnownModel(
            _provider, default, "GPT Test", KnownModelStatus.Available, false, false, true, _limits, null, null));

        exception.ParamName.ShouldBe("modelId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenDisplayNameIsBlank_ThrowsArgumentException(string? displayName) =>
        Should.Throw<ArgumentException>(() => Create(displayName: displayName!)).ParamName.ShouldBe("displayName");

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(status: (KnownModelStatus) 99)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenLimitsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new KnownModel(
            _provider, _model, "GPT Test", KnownModelStatus.Available, false, false, true, null!, null, null))
            .ParamName.ShouldBe("limits");

    [Fact]
    public void Constructor_WhenValid_ExposesEveryValue()
    {
        var pricing = new KnownModelPricing("USD", 1m, 2m, null, null);
        var successor = new ModelId("gpt-next");

        var model = Create(status: KnownModelStatus.Deprecated, pricing: pricing, replacedBy: successor, reasoning: true, vision: true);

        model.ProviderId.ShouldBe(_provider);
        model.ModelId.ShouldBe(_model);
        model.DisplayName.ShouldBe("GPT Test");
        model.Status.ShouldBe(KnownModelStatus.Deprecated);
        model.SupportsReasoning.ShouldBeTrue();
        model.SupportsVisionInput.ShouldBeTrue();
        model.SupportsToolCalls.ShouldBeTrue();
        model.Limits.ShouldBe(_limits);
        model.Pricing.ShouldBe(pricing);
        model.ReplacedBy.ShouldBe(successor);
    }

    [Fact]
    public void ToDescriptor_WhenAliasIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create().ToDescriptor(default, _apiFamily, _baseline)).ParamName.ShouldBe("alias");

    [Fact]
    public void ToDescriptor_WhenApiFamilyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create().ToDescriptor(new ModelAlias("a"), default, _baseline)).ParamName.ShouldBe("apiFamily");

    [Fact]
    public void ToDescriptor_WhenBaselineIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create().ToDescriptor(new ModelAlias("a"), _apiFamily, null!)).ParamName.ShouldBe("baselineCapabilities");

    [Fact]
    public void ToDescriptor_WhenCalled_KeepsProtocolBaselineAndOverlaysModelFacts()
    {
        var model = Create(reasoning: true, vision: true);

        var descriptor = model.ToDescriptor(new ModelAlias("assistant"), _apiFamily, _baseline);

        descriptor.Alias.ShouldBe(new ModelAlias("assistant"));
        descriptor.ProviderId.ShouldBe(_provider);
        descriptor.ApiFamily.ShouldBe(_apiFamily);
        descriptor.ModelId.ShouldBe(_model);
        descriptor.DeploymentId.ShouldBeNull();
        descriptor.Capabilities.SupportsSystemInstructions.ShouldBeTrue();
        descriptor.Capabilities.SupportsStreaming.ShouldBeTrue();
        descriptor.Capabilities.SupportsStructuredOutput.ShouldBeTrue();
        descriptor.Capabilities.SupportsReasoning.ShouldBeTrue();
        descriptor.Capabilities.SupportsVisionInput.ShouldBeTrue();
        descriptor.Capabilities.SupportsToolCalls.ShouldBeTrue();
        descriptor.Capabilities.SupportsParallelToolCalls.ShouldBeTrue();
        descriptor.Limits.ShouldBe(_limits);
        descriptor.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void ToDescriptor_WhenModelDoesNotSupportToolCalls_DisablesParallelToolCallsEvenIfBaselineAllowsThem()
    {
        var descriptor = Create(toolCalls: false).ToDescriptor(new ModelAlias("a"), _apiFamily, _baseline);

        descriptor.Capabilities.SupportsToolCalls.ShouldBeFalse();
        descriptor.Capabilities.SupportsParallelToolCalls.ShouldBeFalse();
    }

    [Fact]
    public void ToDescriptor_WhenBaselineDisallowsParallelToolCalls_DoesNotEnableThem()
    {
        var baseline = _baseline with { SupportsParallelToolCalls = false };

        var descriptor = Create().ToDescriptor(new ModelAlias("a"), _apiFamily, baseline);

        descriptor.Capabilities.SupportsToolCalls.ShouldBeTrue();
        descriptor.Capabilities.SupportsParallelToolCalls.ShouldBeFalse();
    }

    [Fact]
    public void ToDescriptor_WhenBaselineClaimsReasoningTheModelLacks_ModelFactsWin()
    {
        var baseline = _baseline with { SupportsReasoning = true, SupportsVisionInput = true };

        var descriptor = Create(reasoning: false, vision: false).ToDescriptor(new ModelAlias("a"), _apiFamily, baseline);

        descriptor.Capabilities.SupportsReasoning.ShouldBeFalse();
        descriptor.Capabilities.SupportsVisionInput.ShouldBeFalse();
    }

    [Fact]
    public void ToDescriptor_WhenPricingIsAbsent_LeavesDescriptorPricingNull() =>
        Create(pricing: null).ToDescriptor(new ModelAlias("a"), _apiFamily, _baseline).Pricing.ShouldBeNull();

    [Fact]
    public void ToDescriptor_WhenPricingIsPresent_ProjectsInputOutputAndCurrency()
    {
        var descriptor = Create(pricing: new KnownModelPricing("USD", 0.15m, 0.6m, 0.075m, 0m))
            .ToDescriptor(new ModelAlias("a"), _apiFamily, _baseline);

        var pricing = descriptor.Pricing.ShouldNotBeNull();
        pricing.InputCostPerMillionTokens.ShouldBe(0.15m);
        pricing.OutputCostPerMillionTokens.ShouldBe(0.6m);
        pricing.CostCurrency.ShouldBe("USD");
    }

    [Fact]
    public void ToDescriptor_WhenCalledTwiceWithTheSameInputs_ProducesEqualDescriptors()
    {
        var model = Create(pricing: new KnownModelPricing("USD", 1m, 2m, null, null));

        var first = model.ToDescriptor(new ModelAlias("a"), _apiFamily, _baseline);
        var second = model.ToDescriptor(new ModelAlias("a"), _apiFamily, _baseline);

        // Adapters compare the selected descriptor against their own by value, so this equality is load-bearing.
        first.ShouldBe(second);
    }

    [Fact]
    public void WithExpression_WhenCloningWithNoChanges_ProducesEqualButDistinctInstance()
    {
        var original = Create(displayName: "first");

        var copy = original with { };

        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
        copy.DisplayName.ShouldBe("first");
    }

    private static KnownModel Create(
        ProviderId? providerId = null,
        ModelId? modelId = null,
        string displayName = "GPT Test",
        KnownModelStatus status = KnownModelStatus.Available,
        bool reasoning = false,
        bool vision = false,
        bool toolCalls = true,
        ModelLimits? limits = null,
        KnownModelPricing? pricing = null,
        ModelId? replacedBy = null) => new(
            providerId ?? _provider,
            modelId ?? _model,
            displayName,
            status,
            reasoning,
            vision,
            toolCalls,
            limits ?? _limits,
            pricing,
            replacedBy);
}
