// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddContextCompaction_WhenCalled_RegistersCompactorAndCollaborators()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        WithFakeCoordinator(services);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactor>().ShouldBeOfType<DefaultCompactor>();
        _ = provider.GetRequiredService<ICompactionCutSelector>().ShouldBeOfType<StructuralCompactionCutSelector>();
        _ = provider.GetRequiredService<ICompactionStrategy>().ShouldBeOfType<ExtractiveCompactionStrategy>();
        _ = provider.GetRequiredService<ICompactionValidator>().ShouldBeOfType<DefaultCompactionValidator>();
        _ = provider.GetRequiredService<ICompactionSizeEstimator>().ShouldBeOfType<CharacterCompactionSizeEstimator>();
    }

    [Fact]
    public void AddContextCompaction_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        _ = services.AddContextCompaction();
        WithFakeCoordinator(services);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ICompactor>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddContextCompaction_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction(o => o.MaximumSourceEntries = 42);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.MaximumSourceEntries.ShouldBe(42);
    }

    [Fact]
    public void AddContextCompaction_WhenMaximumSourceEntriesIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumSourceEntries = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);
    }

    [Fact]
    public void AddContextCompaction_WhenCharactersPerTokenIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.CharactersPerToken = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    public void AddContextCompaction_WhenMaximumCheckpointCharactersDoesNotExceedTruncationMarker_FailsValidationOnAccess(int maximum)
    {
        // A ceiling at or below the marker length would make the extractive strategy return untruncated text and the
        // validator reject it on every attempt: a deterministically failing pipeline that must fail at composition.
        ExtractiveCompactionStrategy.TruncationMarker.Length.ShouldBe(19);
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumCheckpointCharacters = maximum);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);
    }

    [Fact]
    public void AddContextCompaction_WhenMaximumCheckpointCharactersJustExceedsTruncationMarker_PassesValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumCheckpointCharacters = ExtractiveCompactionStrategy.TruncationMarker.Length + 1);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.MaximumCheckpointCharacters.ShouldBe(20);
    }

    [Fact]
    public void AddContextCompaction_WhenNotConfigured_UsesEmbeddedDefaultSummaryPrompt()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        using var provider = services.BuildServiceProvider();

        var prompt = provider.GetRequiredService<IOptions<CompactionOptions>>().Value.SummaryPrompt;
        prompt.ShouldBe(CompactionPromptResources.DefaultSummaryPrompt);
        prompt.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AddContextCompaction_WhenConfigured_UsesCustomSummaryPrompt()
    {
        const string customPrompt = "Summarize the transcript in three bullet points.";
        var services = new ServiceCollection();

        _ = services.AddContextCompaction(o => o.SummaryPrompt = customPrompt);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.SummaryPrompt.ShouldBe(customPrompt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(null)]
    public void AddContextCompaction_WhenSummaryPromptIsWhitespace_FailsValidation(string? prompt)
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.SummaryPrompt = prompt!);
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);

        exception.Failures.ShouldContain(static failure => failure.Contains("SummaryPrompt", StringComparison.Ordinal));
    }

    [Fact]
    public void AddContextCompaction_WhenMaximumSummaryInputCharactersDoesNotExceedTruncationMarker_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumSummaryInputCharacters = ModelCompactionStrategy.TruncationMarker.Length);
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);

        exception.Failures.ShouldContain(static failure => failure.Contains("MaximumSummaryInputCharacters", StringComparison.Ordinal));
    }

    [Fact]
    public void AddContextCompaction_WhenSummaryModelPolicyIsNull_PassesValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.SummaryModelPolicy.ShouldBeNull();
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenCalled_RegistersModelStrategy()
    {
        var services = new ServiceCollection();

        _ = services.AddModelBackedContextCompaction(o => o.SummaryModelPolicy = SummaryPolicy());
        WithFakeCoordinator(services);
        WithFakeModelRuntime(services);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactionStrategy>().ShouldBeOfType<ModelCompactionStrategy>();
        provider.GetServices<ICompactionStrategy>().Count().ShouldBe(1);
        _ = provider.GetRequiredService<ICompactor>().ShouldBeOfType<DefaultCompactor>();
        _ = provider.GetRequiredService<ICompactionCutSelector>().ShouldBeOfType<StructuralCompactionCutSelector>();
        _ = provider.GetRequiredService<ICompactionValidator>().ShouldBeOfType<DefaultCompactionValidator>();
        _ = provider.GetRequiredService<IIdentifierGenerator<ModelRequestId>>();
        _ = provider.GetRequiredService<IIdentifierGenerator<MessageId>>();
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenCalledAfterAddContextCompaction_ReplacesExtractiveStrategy()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        _ = services.AddModelBackedContextCompaction(o => o.SummaryModelPolicy = SummaryPolicy());
        WithFakeCoordinator(services);
        WithFakeModelRuntime(services);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactionStrategy>().ShouldBeOfType<ModelCompactionStrategy>();
        provider.GetServices<ICompactionStrategy>().Count().ShouldBe(1);
        provider.GetServices<ICompactor>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenCalledTwiceOrFollowedByAddContextCompaction_KeepsSingleModelStrategy()
    {
        var services = new ServiceCollection();

        _ = services.AddModelBackedContextCompaction(o => o.SummaryModelPolicy = SummaryPolicy());
        _ = services.AddModelBackedContextCompaction();
        _ = services.AddContextCompaction();
        WithFakeCoordinator(services);
        WithFakeModelRuntime(services);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactionStrategy>().ShouldBeOfType<ModelCompactionStrategy>();
        provider.GetServices<ICompactionStrategy>().Count().ShouldBe(1);
        provider.GetServices<IIdentifierGenerator<ModelRequestId>>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenSummaryModelPolicyNotConfigured_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddModelBackedContextCompaction();
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);

        exception.Failures.ShouldContain(static failure => failure.Contains("SummaryModelPolicy", StringComparison.Ordinal));
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenSummaryPromptIsWhitespace_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddModelBackedContextCompaction(o =>
        {
            o.SummaryModelPolicy = SummaryPolicy();
            o.SummaryPrompt = " ";
        });
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);

        exception.Failures.ShouldContain(static failure => failure.Contains("SummaryPrompt", StringComparison.Ordinal));
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenConfigured_UsesCustomSummaryPrompt()
    {
        const string customPrompt = "Summarize for the model-backed pipeline.";
        var services = new ServiceCollection();

        _ = services.AddModelBackedContextCompaction(o =>
        {
            o.SummaryModelPolicy = SummaryPolicy();
            o.SummaryPrompt = customPrompt;
        });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.SummaryPrompt.ShouldBe(customPrompt);
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenModelRuntimeNotRegistered_FailsOnStrategyResolution()
    {
        var services = new ServiceCollection();
        _ = services.AddModelBackedContextCompaction(o => o.SummaryModelPolicy = SummaryPolicy());
        WithFakeCoordinator(services);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionStrategy>);
    }

    private static ModelSelectionPolicy SummaryPolicy() => new([new ModelAlias("summarizer")]);

    private static void WithFakeModelRuntime(IServiceCollection services)
    {
        var descriptor = TestFactory.SummaryModel();
        _ = services.AddSingleton<IModelCatalog>(new TestSupport.StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [descriptor])));
        _ = services.AddSingleton<IModelSelector>(TestSupport.ScriptedModelSelector.Selecting(descriptor));
        _ = services.AddSingleton<ILlmModelResolver>(new TestSupport.AliasLlmModelResolver(new TestSupport.ScriptedLlmModel(descriptor.Alias)));
    }

    private static void WithFakeCoordinator(IServiceCollection services) =>
        services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator(new BranchId(Guid.NewGuid())));
}
