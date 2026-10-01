// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class ServiceExtensionsTests
{
    private static readonly ComponentKey<ICompactor> _default = AgentContextCompactionComponentDefaults.CompactorKey;
    private static readonly ComponentKey<ICompactor> _first = new("first");
    private static readonly ComponentKey<ICompactor> _second = new("second");
    private static readonly CompactionProfileKey _profile = new("profile");

    private BranchId BranchId { get; } = new(Guid.NewGuid());

    private FakeSessionCoordinator Coordinator => field ??= new FakeSessionCoordinator(BranchId);

    [Fact]
    public void AddContextCompaction_WhenCalled_RegistersCompactorAndCollaborators()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        WithFakeCoordinator(services);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactor>().ShouldBeOfType<DefaultCompactor>();
        _ = provider.GetRequiredService<ICompactionCutSelector>().ShouldBeOfType<StructuralCompactionCutSelector>();
        _ = provider.GetRequiredService<ICompactionValidator>().ShouldBeOfType<DefaultCompactionValidator>();
        _ = provider.GetRequiredService<ICompactionSizeEstimator>().ShouldBeOfType<CharacterCompactionSizeEstimator>();
        _ = Strategy(provider, _default, CompactionStrategyKeys.Extractive).ShouldBeOfType<ExtractiveCompactionStrategy>();
        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_default.Value)
            .DefaultStrategyOrder.ShouldBe([CompactionStrategyKeys.Extractive]);
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

        _ = Strategy(provider, _default, CompactionStrategyKeys.ModelSummary).ShouldBeOfType<ModelCompactionStrategy>();
        _ = Strategy(provider, _default, CompactionStrategyKeys.Extractive).ShouldBeOfType<ExtractiveCompactionStrategy>();
        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_default.Value)
            .DefaultStrategyOrder.ShouldBe([CompactionStrategyKeys.ModelSummary]);
        _ = provider.GetRequiredService<ICompactor>().ShouldBeOfType<DefaultCompactor>();
        _ = provider.GetRequiredService<ICompactionCutSelector>().ShouldBeOfType<StructuralCompactionCutSelector>();
        _ = provider.GetRequiredService<ICompactionValidator>().ShouldBeOfType<DefaultCompactionValidator>();
        _ = provider.GetRequiredService<IIdentifierGenerator<ModelRequestId>>();
        _ = provider.GetRequiredService<IIdentifierGenerator<MessageId>>();
    }

    [Fact]
    public void AddModelBackedContextCompaction_WhenCalledAfterAddContextCompaction_SwitchesTheDefaultStrategyToTheModelBackedOne()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        _ = services.AddModelBackedContextCompaction(o => o.SummaryModelPolicy = SummaryPolicy());
        WithFakeCoordinator(services);
        WithFakeModelRuntime(services);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_default.Value)
            .DefaultStrategyOrder.ShouldBe([CompactionStrategyKeys.ModelSummary]);
        _ = Strategy(provider, _default, CompactionStrategyKeys.ModelSummary).ShouldBeOfType<ModelCompactionStrategy>();
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

        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_default.Value)
            .DefaultStrategyOrder.ShouldBe([CompactionStrategyKeys.ModelSummary]);
        services.Count(static d => d.ImplementationInstance is CompactionStrategyDeclaration).ShouldBe(2);
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

        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredKeyedService<ICompactionStrategy>(
                CompactionServiceKeys.Strategy(_default, CompactionStrategyKeys.ModelSummary)));
    }

    [Fact]
    public void AddAgentContextCompaction_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddAgentContextCompaction(_first))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddAgentContextCompaction_WhenKeyIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddAgentContextCompaction(default))
            .ParamName.ShouldBe("compactorKey");

    [Fact]
    public void AddAgentContextCompaction_WhenCalledWithoutAddContextCompaction_RegistersAWorkingKeyedCompactor()
    {
        var services = Services();

        _ = services.AddAgentContextCompaction(_first);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<ICompactor>(_first.Value).ShouldBeOfType<DefaultCompactor>();
        provider.GetService<ICompactor>().ShouldBeNull();
        _ = Strategy(provider, _first, CompactionStrategyKeys.Extractive).ShouldBeOfType<ExtractiveCompactionStrategy>();
    }

    [Fact]
    public void AddAgentContextCompaction_WhenKeyIsDefault_AlsoExposesTheCompactorUnkeyed()
    {
        var services = Services();

        _ = services.AddAgentContextCompaction(_default);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICompactor>().ShouldBeSameAs(provider.GetRequiredKeyedService<ICompactor>(_default.Value));
    }

    [Fact]
    public void AddAgentContextCompaction_WhenTwoKeysAreRegistered_KeepsEachKeysOptionsIsolated()
    {
        var services = Services();

        _ = services.AddAgentContextCompaction(_first, o => o.MaximumAttempts = 5);
        _ = services.AddAgentContextCompaction(_second, o => o.MaximumAttempts = 7);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_first.Value).MaximumAttempts.ShouldBe(5);
        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_second.Value).MaximumAttempts.ShouldBe(7);
        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_second.Value).CompactorKey.ShouldBe(_second);
        provider.GetRequiredKeyedService<ICompactor>(_first.Value)
            .ShouldNotBeSameAs(provider.GetRequiredKeyedService<ICompactor>(_second.Value));
    }

    [Fact]
    public void AddAgentContextCompaction_WhenCalledTwiceForOneKey_KeepsASingleCompactorRegistration()
    {
        var services = Services();

        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_first);

        services.Count(static d => d.IsKeyedService && d.ServiceType == typeof(ICompactor)).ShouldBe(1);
        services.Count(static d => d.ImplementationInstance is CompactionStrategyDeclaration).ShouldBe(1);
    }

    [Theory]
    [InlineData("attempts")]
    [InlineData("entries")]
    [InlineData("bytes")]
    [InlineData("tokens")]
    [InlineData("retained")]
    [InlineData("issues")]
    [InlineData("ratio-zero")]
    [InlineData("ratio-one")]
    [InlineData("timeout")]
    [InlineData("order-empty")]
    [InlineData("order-duplicate")]
    [InlineData("order-default")]
    public void AddAgentContextCompaction_WhenAnOptionIsInvalid_FailsValidationOnAccess(string invalid)
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first, options =>
        {
            switch (invalid)
            {
                case "attempts": options.MaximumAttempts = 0; break;
                case "entries": options.MaximumSourceEntries = 0; break;
                case "bytes": options.MaximumSourceBytes = 0; break;
                case "tokens": options.MaximumSummaryTokens = 0; break;
                case "retained": options.MinimumRetainedEntries = -1; break;
                case "issues": options.MaximumValidationIssues = 0; break;
                case "ratio-zero": options.MinimumReductionRatio = 0; break;
                case "ratio-one": options.MinimumReductionRatio = 1; break;
                case "timeout": options.AttemptTimeout = TimeSpan.Zero; break;
                case "order-empty": options.DefaultStrategyOrder = []; break;
                case "order-duplicate": options.DefaultStrategyOrder = [CompactionStrategyKeys.Extractive, CompactionStrategyKeys.Extractive]; break;
                default: options.DefaultStrategyOrder = [default]; break;
            }
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_first.Value));
    }

    [Fact]
    public void AddAgentContextCompaction_WhenAMinimumRetainedEntriesIsZero_PassesValidation()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first, options => options.MinimumRetainedEntries = 0);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(_first.Value).MinimumRetainedEntries.ShouldBe(0);
    }

    [Fact]
    public void AddAgentContextCompaction_WhenTheDefaultOrderNamesAnUnregisteredStrategy_FailsWhenTheCompactorIsResolved()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first, options => options.DefaultStrategyOrder = [new CompactionStrategyKey("missing")]);
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(() => provider.GetRequiredKeyedService<ICompactor>(_first.Value));

        failure.Message.ShouldContain("missing");
    }

    [Fact]
    public void AddCompactionProfile_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddCompactionProfile(_profile, _first, _ => { }))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddCompactionProfile_WhenProfileIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddCompactionProfile(default, _first, _ => { }))
            .ParamName.ShouldBe("profile");

    [Fact]
    public void AddCompactionProfile_WhenCompactorIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddCompactionProfile(_profile, default, _ => { }))
            .ParamName.ShouldBe("compactor");

    [Fact]
    public void AddCompactionProfile_WhenConfigureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddCompactionProfile(_profile, _first, null!))
            .ParamName.ShouldBe("configure");

    [Fact]
    public void AddCompactionProfile_WhenCalled_ReturnsTheSameCollectionAndRegistersNothingResolved()
    {
        var services = new ServiceCollection();

        var returned = services.AddCompactionProfile(_profile, _first, _ => { });

        returned.ShouldBeSameAs(services);
        services.ShouldContain(static d => d.ServiceType == typeof(ICompactionProfileCatalog));
    }

    [Fact]
    public void AddCompactionProfile_WhenTheSameKeyIsRegisteredTwice_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddCompactionProfile(_profile, _first, _ => { });

        var failure = Should.Throw<InvalidOperationException>(
            () => services.AddCompactionProfile(_profile, _second, _ => { }));

        failure.Message.ShouldContain(_profile.Value);
        services.Count(static d => d.ImplementationInstance is CompactionProfileDeclaration).ShouldBe(1);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("default")]
    [InlineData("duplicate")]
    public void AddCompactionProfile_WhenTheConfiguredOptionsAreInvalid_ThrowsInvalidOperationExceptionAndRegistersNothing(string invalid)
    {
        var services = new ServiceCollection();

        var failure = Should.Throw<InvalidOperationException>(() => services.AddCompactionProfile(_profile, _first, options =>
        {
            switch (invalid)
            {
                case "version": options.Version = default; break;
                case "empty": options.StrategyOrder = []; break;
                case "null": options.StrategyOrder = null!; break;
                case "default": options.StrategyOrder = [default]; break;
                default: options.StrategyOrder = [CompactionStrategyKeys.Extractive, CompactionStrategyKeys.Extractive]; break;
            }
        }));

        failure.Message.ShouldContain(_profile.Value);
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddCompactionProfile_WhenTheCompactorAndStrategiesAreRegistered_PublishesTheCompiledPolicy()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first, options => options.MaximumAttempts = 4);
        _ = services.AddCompactionProfile(_profile, _first, options =>
        {
            options.Version = new CompactionProfileVersion(3);
            options.StrategyOrder = [CompactionStrategyKeys.Extractive];
        });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out var publication).ShouldBeTrue();

        publication.ShouldNotBeNull().Enabled.ShouldBeTrue();
        publication.Policy.ProfileKey.ShouldBe(_profile);
        publication.Policy.ProfileVersion.ShouldBe(new CompactionProfileVersion(3));
        publication.Policy.CompactorKey.ShouldBe(_first);
        publication.Policy.StrategyOrder.ShouldBe([CompactionStrategyKeys.Extractive]);
        publication.Policy.MaximumAttempts.ShouldBe(4);
        publication.Policy.AllowOversizedTurnRepair.ShouldBeFalse();
        publication.Policy.ConfigurationFingerprint.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void AddCompactionProfile_WhenTheProfileIsDisabled_PublishesADisabledProfile()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionProfile(_profile, _first, options => options.Enabled = false);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out var publication);

        publication.ShouldNotBeNull().Enabled.ShouldBeFalse();
    }

    [Fact]
    public void AddCompactionProfile_WhenTheOptionsInstanceChangesAfterRegistration_KeepsTheCapturedValues()
    {
        CompactionProfileOptions? captured = null;
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionProfile(_profile, _first, options => captured = options);
        captured!.AllowOversizedTurnRepair = true;
        captured.StrategyOrder.Clear();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out var publication);

        publication.ShouldNotBeNull().Policy.AllowOversizedTurnRepair.ShouldBeFalse();
        publication.Policy.StrategyOrder.ShouldBe([CompactionStrategyKeys.Extractive]);
    }

    [Fact]
    public void AddCompactionProfile_WhenTwoProfilesShareACompactor_PublishesDistinctPolicies()
    {
        var other = new CompactionProfileKey("other-profile");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionProfile(_profile, _first, _ => { });
        _ = services.AddCompactionProfile(other, _first, options => options.Version = new CompactionProfileVersion(2));
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ICompactionProfileCatalog>();

        _ = catalog.TryGet(_profile, out var one);
        _ = catalog.TryGet(other, out var two);

        one.ShouldNotBeNull().Policy.ConfigurationFingerprint.ShouldNotBe(two.ShouldNotBeNull().Policy.ConfigurationFingerprint);
        two.Policy.ProfileVersion.ShouldBe(new CompactionProfileVersion(2));
    }

    [Fact]
    public void AddCompactionProfile_WhenTheProfileIsRegisteredBeforeItsCompactor_StillResolves()
    {
        var services = Services();
        _ = services.AddCompactionProfile(_profile, _first, _ => { });
        _ = services.AddAgentContextCompaction(_first);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out _).ShouldBeTrue();
    }

    [Fact]
    public void AddCompactionProfile_WhenTheCompactorIsNotRegistered_FailsWhenTheCatalogIsBuilt()
    {
        var services = Services();
        _ = services.AddCompactionProfile(_profile, _first, _ => { });
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionProfileCatalog>);

        failure.Message.ShouldContain(_first.Value);
    }

    [Fact]
    public void AddCompactionProfile_WhenAStrategyIsRegisteredOnlyForAnotherCompactor_FailsWhenTheCatalogIsBuilt()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_second, Registration("declining"));
        _ = services.AddCompactionProfile(_profile, _first, options => options.StrategyOrder = [new CompactionStrategyKey("declining")]);
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionProfileCatalog>);

        failure.Message.ShouldContain("declining");
    }

    [Fact]
    public void AddCompactionProfile_WhenAStrategyRequiresAnUnregisteredGenerator_FailsWhenTheCatalogIsBuilt()
    {
        var generatorKey = new CompactionSummaryGeneratorKey("test.generator");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration("declining", generator: generatorKey));
        _ = services.AddCompactionProfile(_profile, _first, options => options.StrategyOrder = [new CompactionStrategyKey("declining")]);
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionProfileCatalog>);

        failure.Message.ShouldContain(generatorKey.Value);
    }

    [Fact]
    public void AddCompactionProfile_WhenTheRequiredGeneratorIsRegistered_Resolves()
    {
        var generatorKey = new CompactionSummaryGeneratorKey("test.generator");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration("declining", generator: generatorKey));
        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, GeneratorRegistration(generatorKey));
        _ = services.AddCompactionProfile(_profile, _first, options => options.StrategyOrder = [new CompactionStrategyKey("declining")]);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out _).ShouldBeTrue();
    }

    [Fact]
    public void AddCompactionProfile_WhenOversizedRepairIsAllowedOnAStrategyWithoutTheCapability_FailsWhenTheCatalogIsBuilt()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionProfile(_profile, _first, options => options.AllowOversizedTurnRepair = true);
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionProfileCatalog>);

        failure.Message.ShouldContain("oversized");
    }

    [Fact]
    public void AddCompactionProfile_WhenOversizedRepairIsAllowedOnAStrategyWithTheCapability_Resolves()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration("repairing", capabilities: CompactionStrategyCapabilities.OversizedTurnRepair));
        _ = services.AddCompactionProfile(_profile, _first, options =>
        {
            options.AllowOversizedTurnRepair = true;
            options.StrategyOrder = [new CompactionStrategyKey("repairing")];
        });
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out var publication);

        publication.ShouldNotBeNull().Policy.AllowOversizedTurnRepair.ShouldBeTrue();
    }

    [Fact]
    public void AddCompactionProfile_WhenTheOrderContradictsABeforeConstraint_FailsWhenTheCatalogIsBuilt()
    {
        var early = new CompactionStrategyKey("early");
        var late = new CompactionStrategyKey("late");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("early"));
        _ = services.AddCompactionStrategy<OtherDecliningCompactionStrategy>(_first, Registration("late", before: [early]));
        _ = services.AddCompactionProfile(_profile, _first, options => options.StrategyOrder = [late, early]);
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionProfileCatalog>);

        failure.Message.ShouldContain("contradicts");
    }

    [Fact]
    public void AddCompactionProfile_WhenTheOrderHonorsBeforeAndAfterConstraints_Resolves()
    {
        var early = new CompactionStrategyKey("early");
        var late = new CompactionStrategyKey("late");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("early", after: [late]));
        _ = services.AddCompactionStrategy<OtherDecliningCompactionStrategy>(_first, Registration("late", before: [early]));
        _ = services.AddCompactionProfile(_profile, _first, options => options.StrategyOrder = [early, late]);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out _).ShouldBeTrue();
    }

    [Fact]
    public void AddCompactionProfile_WhenStrategyConstraintsFormACycle_FailsWhenTheCatalogIsBuilt()
    {
        var a = new CompactionStrategyKey("a");
        var b = new CompactionStrategyKey("b");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("a", before: [b]));
        _ = services.AddCompactionStrategy<OtherDecliningCompactionStrategy>(_first, Registration("b", before: [a]));
        _ = services.AddCompactionProfile(_profile, _first, _ => { });
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ICompactionProfileCatalog>);

        failure.Message.ShouldContain("cycle");
    }

    [Fact]
    public void AddCompactionProfile_WhenAConstraintNamesAnAbsentStrategy_IgnoresIt()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration("a", before: [new CompactionStrategyKey("absent")]));
        _ = services.AddCompactionProfile(_profile, _first, _ => { });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(_profile, out _).ShouldBeTrue();
    }

    [Fact]
    public void AddCompactionStrategy_WhenArgumentsAreInvalid_ThrowsExactExceptions()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("x")))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddCompactionStrategy<DecliningCompactionStrategy>(default, Registration("x")))
            .ParamName.ShouldBe("compactor");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddCompactionStrategy<DecliningCompactionStrategy>(_first, null!))
            .ParamName.ShouldBe("registration");
    }

    [Fact]
    public void AddCompactionStrategy_WhenTheLifetimeIsScoped_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration("x", ServiceLifetime.Scoped))).ParamName.ShouldBe("registration");
        services.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    public async Task AddCompactionStrategy_WhenCalled_ResolvesTheStrategyUnderThatCompactorAndKeyOnly(ServiceLifetime lifetime)
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", lifetime));
        using var provider = services.BuildServiceProvider();

        _ = Strategy(provider, _first, new CompactionStrategyKey("declining")).ShouldBeOfType<DecliningCompactionStrategy>();
        var other = await provider.GetRequiredKeyedService<ICompactionStrategyResolver>(CompactionServiceKeys.StrategyResolver(_second))
            .ResolveAsync(_second, new CompactionStrategyKey("declining"), TestContext.Current.CancellationToken);
        _ = other.ShouldBeOfType<CompactionStrategyNotFound>();
    }

    [Fact]
    public void AddCompactionStrategy_WhenTheTransientLifetimeIsRegistered_ResolvesAFreshInstancePerLookup()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", ServiceLifetime.Transient));
        using var provider = services.BuildServiceProvider();

        Strategy(provider, _first, new CompactionStrategyKey("declining"))
            .ShouldNotBeSameAs(Strategy(provider, _first, new CompactionStrategyKey("declining")));
    }

    [Fact]
    public void AddCompactionStrategy_WhenAnEquivalentRegistrationRepeats_IsIdempotent()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", before: [CompactionStrategyKeys.Extractive]));
        var count = services.Count;

        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", before: [CompactionStrategyKeys.Extractive]));

        services.Count.ShouldBe(count);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("order")]
    [InlineData("lifetime")]
    [InlineData("after")]
    public void AddCompactionStrategy_WhenADuplicateKeyDiffers_ThrowsInvalidOperationException(string differs)
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining"));

        var failure = Should.Throw<InvalidOperationException>(() => _ = differs switch
        {
            "type" => services.AddCompactionStrategy<OtherDecliningCompactionStrategy>(_first, Registration("declining")),
            "order" => services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", order: 9)),
            "lifetime" => services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", ServiceLifetime.Transient)),
            _ => services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining", after: [CompactionStrategyKeys.Extractive])),
        });

        failure.Message.ShouldContain("ReplaceCompactionStrategy");
    }

    [Fact]
    public void AddCompactionStrategy_WhenItReusesAFirstPartyKey_ThrowsInvalidOperationException()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);

        _ = Should.Throw<InvalidOperationException>(() => services.AddCompactionStrategy<DecliningCompactionStrategy>(
            _first, new CompactionStrategyRegistration(
                new CompactionStrategyDescriptor(
                    CompactionStrategyKeys.Extractive, new CompactionStrategyVersion("9"), CompactionStrategyCapabilities.None, true, null),
                0, [], [], ServiceLifetime.Singleton)));
    }

    [Fact]
    public void ReplaceCompactionStrategy_WhenTheKeyIsRegistered_ReplacesOnlyThatCompactorsStrategy()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("declining"));
        _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(_second, Registration("declining"));

        _ = services.ReplaceCompactionStrategy<OtherDecliningCompactionStrategy>(_first, Registration("declining", order: 4));
        using var provider = services.BuildServiceProvider();

        _ = Strategy(provider, _first, new CompactionStrategyKey("declining")).ShouldBeOfType<OtherDecliningCompactionStrategy>();
        _ = Strategy(provider, _second, new CompactionStrategyKey("declining")).ShouldBeOfType<DecliningCompactionStrategy>();
        services.Count(static d => d.ImplementationInstance is CompactionStrategyDeclaration { Registration.Descriptor.Key.Value: "declining" }).ShouldBe(2);
    }

    [Fact]
    public void ReplaceCompactionStrategy_WhenTheFirstPartyStrategyIsReplaced_ResolvesTheReplacement()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.ReplaceCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration(CompactionStrategyKeys.Extractive.Value));
        using var provider = services.BuildServiceProvider();

        _ = Strategy(provider, _first, CompactionStrategyKeys.Extractive).ShouldBeOfType<DecliningCompactionStrategy>();
    }

    [Fact]
    public void ReplaceCompactionStrategy_WhenCalledBeforeTheCompactorIsRegistered_IsKeptByTheLaterRegistration()
    {
        var services = Services();
        _ = services.ReplaceCompactionStrategy<DecliningCompactionStrategy>(
            _first, Registration(CompactionStrategyKeys.Extractive.Value));
        _ = services.AddAgentContextCompaction(_first);
        using var provider = services.BuildServiceProvider();

        _ = Strategy(provider, _first, CompactionStrategyKeys.Extractive).ShouldBeOfType<DecliningCompactionStrategy>();
    }

    [Fact]
    public void ReplaceCompactionStrategy_WhenArgumentsAreInvalid_ThrowsExactExceptions()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).ReplaceCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("x")))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().ReplaceCompactionStrategy<DecliningCompactionStrategy>(default, Registration("x")))
            .ParamName.ShouldBe("compactor");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().ReplaceCompactionStrategy<DecliningCompactionStrategy>(_first, null!))
            .ParamName.ShouldBe("registration");
        Should.Throw<ArgumentException>(() => new ServiceCollection().ReplaceCompactionStrategy<DecliningCompactionStrategy>(_first, Registration("x", ServiceLifetime.Scoped)))
            .ParamName.ShouldBe("registration");
    }

    [Fact]
    public async Task AddCompactionSummaryGenerator_WhenCalled_ResolvesTheGeneratorUnderThatCompactorOnly()
    {
        var key = new CompactionSummaryGeneratorKey("test.generator");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, GeneratorRegistration(key));
        using var provider = services.BuildServiceProvider();

        var resolved = await provider.GetRequiredKeyedService<ICompactionSummaryGeneratorResolver>(
            CompactionServiceKeys.SummaryGeneratorResolver(_first)).ResolveAsync(_first, key, TestContext.Current.CancellationToken);
        var missing = await provider.GetRequiredKeyedService<ICompactionSummaryGeneratorResolver>(
            CompactionServiceKeys.SummaryGeneratorResolver(_second)).ResolveAsync(_second, key, TestContext.Current.CancellationToken);

        _ = resolved.ShouldBeOfType<CompactionSummaryGeneratorResolved>().Generator.ShouldBeOfType<FailingSummaryGenerator>();
        _ = missing.ShouldBeOfType<CompactionSummaryGeneratorNotFound>();
    }

    [Fact]
    public void AddCompactionSummaryGenerator_WhenArgumentsAreInvalid_ThrowsExactExceptions()
    {
        var registration = GeneratorRegistration(new CompactionSummaryGeneratorKey("g"));
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, registration))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddCompactionSummaryGenerator<FailingSummaryGenerator>(default, registration))
            .ParamName.ShouldBe("compactor");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, null!))
            .ParamName.ShouldBe("registration");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddCompactionSummaryGenerator<FailingSummaryGenerator>(
            _first, GeneratorRegistration(new CompactionSummaryGeneratorKey("g"), ServiceLifetime.Scoped))).ParamName.ShouldBe("registration");
    }

    [Fact]
    public void AddCompactionSummaryGenerator_WhenAnEquivalentRegistrationRepeats_IsIdempotent()
    {
        var key = new CompactionSummaryGeneratorKey("test.generator");
        var services = Services();
        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, GeneratorRegistration(key));
        var count = services.Count;

        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, GeneratorRegistration(key));

        services.Count.ShouldBe(count);
    }

    [Fact]
    public void AddCompactionSummaryGenerator_WhenADuplicateKeyDiffers_ThrowsInvalidOperationException()
    {
        var key = new CompactionSummaryGeneratorKey("test.generator");
        var services = Services();
        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, GeneratorRegistration(key));

        var failure = Should.Throw<InvalidOperationException>(
            () => services.AddCompactionSummaryGenerator<OtherFailingSummaryGenerator>(_first, GeneratorRegistration(key)));

        failure.Message.ShouldContain("ReplaceCompactionSummaryGenerator");
    }

    [Fact]
    public async Task ReplaceCompactionSummaryGenerator_WhenCalled_ReplacesOnlyThatCompactorsGenerator()
    {
        var key = new CompactionSummaryGeneratorKey("test.generator");
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_first, GeneratorRegistration(key));
        _ = services.AddCompactionSummaryGenerator<FailingSummaryGenerator>(_second, GeneratorRegistration(key));

        _ = services.ReplaceCompactionSummaryGenerator<OtherFailingSummaryGenerator>(_first, GeneratorRegistration(key));
        using var provider = services.BuildServiceProvider();

        var first = await provider.GetRequiredKeyedService<ICompactionSummaryGeneratorResolver>(
            CompactionServiceKeys.SummaryGeneratorResolver(_first)).ResolveAsync(_first, key, TestContext.Current.CancellationToken);
        var second = await provider.GetRequiredKeyedService<ICompactionSummaryGeneratorResolver>(
            CompactionServiceKeys.SummaryGeneratorResolver(_second)).ResolveAsync(_second, key, TestContext.Current.CancellationToken);
        _ = first.ShouldBeOfType<CompactionSummaryGeneratorResolved>().Generator.ShouldBeOfType<OtherFailingSummaryGenerator>();
        _ = second.ShouldBeOfType<CompactionSummaryGeneratorResolved>().Generator.ShouldBeOfType<FailingSummaryGenerator>();
    }

    [Fact]
    public void ReplaceCompactionSummaryGenerator_WhenArgumentsAreInvalid_ThrowsExactExceptions()
    {
        var registration = GeneratorRegistration(new CompactionSummaryGeneratorKey("g"));
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).ReplaceCompactionSummaryGenerator<FailingSummaryGenerator>(_first, registration))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().ReplaceCompactionSummaryGenerator<FailingSummaryGenerator>(default, registration))
            .ParamName.ShouldBe("compactor");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().ReplaceCompactionSummaryGenerator<FailingSummaryGenerator>(_first, null!))
            .ParamName.ShouldBe("registration");
    }

    [Fact]
    public async Task AddCompactionEventSink_WhenSinksAreRegistered_DeliversInOrderThenIdentityToThatCompactorOnly()
    {
        var log = new CompactionEventLog();
        var services = Services();
        _ = services.AddSingleton(log);
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = services.AddCompactionEventSink<OtherRecordingCompactionEventSink>(_first, SinkRegistration("b", order: 1));
        _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("a", order: 1));
        _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("z", order: 0));
        using var provider = services.BuildServiceProvider();

        var firstResult = await Dispatcher(provider, _first).PublishAsync(_first, Event(), TestContext.Current.CancellationToken);
        var secondResult = await Dispatcher(provider, _second).PublishAsync(_second, Event(), TestContext.Current.CancellationToken);

        _ = firstResult.ShouldBeOfType<CompactionEventPublished>();
        _ = secondResult.ShouldBeOfType<CompactionEventPublished>();
        log.Entries.ShouldBe(
            [nameof(RecordingCompactionEventSink), nameof(RecordingCompactionEventSink), nameof(OtherRecordingCompactionEventSink)]);
    }

    [Fact]
    public async Task AddCompactionEventSink_WhenARequiredSinkThrows_ReportsTheRequiredSinkUnavailable()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionEventSink<ThrowingCompactionEventSink>(
            _first, SinkRegistration("audit", delivery: CompactionEventDelivery.Required));
        using var provider = services.BuildServiceProvider();

        var result = await Dispatcher(provider, _first).PublishAsync(_first, Event(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RequiredCompactionEventUnavailable>().SinkId.ShouldBe(new CompactionEventSinkId("audit"));
    }

    [Fact]
    public async Task AddCompactionEventSink_WhenABestEffortSinkThrows_StillReportsPublished()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionEventSink<ThrowingCompactionEventSink>(_first, SinkRegistration("audit"));
        using var provider = services.BuildServiceProvider();

        var result = await Dispatcher(provider, _first).PublishAsync(_first, Event(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionEventPublished>();
    }

    [Fact]
    public void AddCompactionEventSink_WhenArgumentsAreInvalid_ThrowsExactExceptions()
    {
        var registration = SinkRegistration("s");
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddCompactionEventSink<RecordingCompactionEventSink>(_first, registration))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddCompactionEventSink<RecordingCompactionEventSink>(default, registration))
            .ParamName.ShouldBe("compactor");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddCompactionEventSink<RecordingCompactionEventSink>(_first, null!))
            .ParamName.ShouldBe("registration");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddCompactionEventSink<RecordingCompactionEventSink>(
            _first, SinkRegistration("s", lifetime: ServiceLifetime.Scoped))).ParamName.ShouldBe("registration");
    }

    [Fact]
    public void AddCompactionEventSink_WhenAnEquivalentRegistrationRepeats_IsIdempotent()
    {
        var services = Services();
        _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("s"));
        var count = services.Count;

        _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("s"));

        services.Count.ShouldBe(count);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("order")]
    [InlineData("delivery")]
    public void AddCompactionEventSink_WhenADuplicateIdentityDiffers_ThrowsInvalidOperationException(string differs)
    {
        var services = Services();
        _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("s"));

        var failure = Should.Throw<InvalidOperationException>(() => _ = differs switch
        {
            "type" => services.AddCompactionEventSink<OtherRecordingCompactionEventSink>(_first, SinkRegistration("s")),
            "order" => services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("s", order: 3)),
            _ => services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("s", delivery: CompactionEventDelivery.Required)),
        });

        failure.Message.ShouldContain("ReplaceCompactionEventSink");
    }

    [Fact]
    public async Task ReplaceCompactionEventSink_WhenCalled_ReplacesTheSinkForThatIdentity()
    {
        var log = new CompactionEventLog();
        var services = Services();
        _ = services.AddSingleton(log);
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_first, SinkRegistration("s"));
        _ = services.ReplaceCompactionEventSink<OtherRecordingCompactionEventSink>(_first, SinkRegistration("s", order: 2));
        using var provider = services.BuildServiceProvider();

        _ = await Dispatcher(provider, _first).PublishAsync(_first, Event(), TestContext.Current.CancellationToken);

        log.Entries.ShouldBe([nameof(OtherRecordingCompactionEventSink)]);
        services.Count(static d => d.ImplementationInstance is CompactionEventSinkDeclaration).ShouldBe(1);
    }

    [Fact]
    public void ReplaceCompactionEventSink_WhenArgumentsAreInvalid_ThrowsExactExceptions()
    {
        var registration = SinkRegistration("s");
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).ReplaceCompactionEventSink<RecordingCompactionEventSink>(_first, registration))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().ReplaceCompactionEventSink<RecordingCompactionEventSink>(default, registration))
            .ParamName.ShouldBe("compactor");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().ReplaceCompactionEventSink<RecordingCompactionEventSink>(_first, null!))
            .ParamName.ShouldBe("registration");
    }

    public enum ReplaceKind
    {
        Compactor,
        CutSelector,
        Validator,
        StrategyResolver,
        SummaryGeneratorResolver,
        ActivationCoordinator,
        EventDispatcher,
    }

    [Theory]
    [InlineData(ReplaceKind.Compactor)]
    [InlineData(ReplaceKind.CutSelector)]
    [InlineData(ReplaceKind.Validator)]
    [InlineData(ReplaceKind.StrategyResolver)]
    [InlineData(ReplaceKind.SummaryGeneratorResolver)]
    [InlineData(ReplaceKind.ActivationCoordinator)]
    [InlineData(ReplaceKind.EventDispatcher)]
    public void Replace_WhenCalledForOneCompactorKey_ReplacesOnlyThatKeysCollaborator(ReplaceKind kind)
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddAgentContextCompaction(_second);
        _ = ReplaceCollaborator(services, kind, _first);
        using var provider = services.BuildServiceProvider();

        var replaced = Collaborator(provider, kind, _first);
        var untouched = Collaborator(provider, kind, _second);

        replaced.GetType().Name.ShouldStartWith("Replacement");
        untouched.GetType().Name.ShouldNotStartWith("Replacement");
    }

    [Theory]
    [InlineData(ReplaceKind.Compactor)]
    [InlineData(ReplaceKind.CutSelector)]
    [InlineData(ReplaceKind.Validator)]
    [InlineData(ReplaceKind.StrategyResolver)]
    [InlineData(ReplaceKind.SummaryGeneratorResolver)]
    [InlineData(ReplaceKind.ActivationCoordinator)]
    [InlineData(ReplaceKind.EventDispatcher)]
    public void Replace_WhenCalledBeforeTheCompactorIsRegistered_IsKeptByTheLaterRegistration(ReplaceKind kind)
    {
        var services = Services();
        _ = ReplaceCollaborator(services, kind, _first);
        _ = services.AddAgentContextCompaction(_first);
        using var provider = services.BuildServiceProvider();

        Collaborator(provider, kind, _first).GetType().Name.ShouldStartWith("Replacement");
    }

    [Theory]
    [InlineData(ReplaceKind.Compactor)]
    [InlineData(ReplaceKind.CutSelector)]
    [InlineData(ReplaceKind.Validator)]
    [InlineData(ReplaceKind.StrategyResolver)]
    [InlineData(ReplaceKind.SummaryGeneratorResolver)]
    [InlineData(ReplaceKind.ActivationCoordinator)]
    [InlineData(ReplaceKind.EventDispatcher)]
    public void Replace_WhenCalledTwice_KeepsASingleRegistration(ReplaceKind kind)
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = ReplaceCollaborator(services, kind, _first);
        var count = services.Count;

        _ = ReplaceCollaborator(services, kind, _first);

        services.Count.ShouldBe(count);
    }

    [Theory]
    [InlineData(ReplaceKind.Compactor)]
    [InlineData(ReplaceKind.CutSelector)]
    [InlineData(ReplaceKind.Validator)]
    [InlineData(ReplaceKind.StrategyResolver)]
    [InlineData(ReplaceKind.SummaryGeneratorResolver)]
    [InlineData(ReplaceKind.ActivationCoordinator)]
    [InlineData(ReplaceKind.EventDispatcher)]
    public void Replace_WhenArgumentsAreInvalid_ThrowsExactExceptions(ReplaceKind kind)
    {
        Should.Throw<ArgumentNullException>(() => ReplaceCollaborator(null!, kind, _first)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => ReplaceCollaborator(new ServiceCollection(), kind, default))
            .ParamName.ShouldBeOneOf("key", "compactor");
    }

    [Fact]
    public void ReplaceCompactor_WhenTheDefaultKeyIsReplaced_TheUnkeyedRegistrationResolvesTheReplacement()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_default);
        _ = services.ReplaceCompactor<ReplacementCompactor>(_default);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactor>().ShouldBeOfType<ReplacementCompactor>();
    }

    [Fact]
    public void ReplaceCompactionCutSelector_WhenTheUnkeyedDefaultIsReplaced_UnreplacedCompactorsFollowIt()
    {
        var services = Services();
        _ = services.AddAgentContextCompaction(_first);
        _ = services.AddSingleton<ICompactionCutSelector>(new ReplacementCutSelector());
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<ICompactionCutSelector>(CompactionServiceKeys.CutSelector(_first))
            .ShouldBeOfType<ReplacementCutSelector>();
    }

    private static CompactionStrategyRegistration Registration(
        string key,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        int order = 0,
        CompactionStrategyKey[]? before = null,
        CompactionStrategyKey[]? after = null,
        CompactionStrategyCapabilities capabilities = CompactionStrategyCapabilities.None,
        CompactionSummaryGeneratorKey? generator = null) =>
        new(
            new CompactionStrategyDescriptor(
                new CompactionStrategyKey(key), new CompactionStrategyVersion("1"), capabilities, deterministic: true, generator),
            order,
            [.. before ?? []],
            [.. after ?? []],
            lifetime);

    private static CompactionSummaryGeneratorRegistration GeneratorRegistration(
        CompactionSummaryGeneratorKey key, ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        new(
            new CompactionSummaryGeneratorDescriptor(
                key, new CompactionSummaryGeneratorVersion("1"), modelBacked: false, deterministic: true, 1, 1),
            lifetime);

    private static CompactionEventSinkRegistration SinkRegistration(
        string id,
        int order = 0,
        CompactionEventDelivery delivery = CompactionEventDelivery.BestEffort,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        new(new CompactionEventSinkId(id), order, delivery, lifetime);

    private static CompactionAttemptStartedEvent Event() =>
        new(
            TestFactory.CompactionContext(),
            DateTimeOffset.UnixEpoch,
            new CompactionTrigger(CompactionTriggerKind.ExplicitMaintenance, "test", null));

    private static ICompactionEventDispatcher Dispatcher(IServiceProvider provider, ComponentKey<ICompactor> key) =>
        provider.GetRequiredKeyedService<ICompactionEventDispatcher>(CompactionServiceKeys.EventDispatcher(key));

    private static ICompactionStrategy Strategy(IServiceProvider provider, ComponentKey<ICompactor> compactor, CompactionStrategyKey key) =>
        provider.GetRequiredKeyedService<ICompactionStrategy>(CompactionServiceKeys.Strategy(compactor, key));

    private static IServiceCollection ReplaceCollaborator(IServiceCollection services, ReplaceKind kind, ComponentKey<ICompactor> key) => kind switch
    {
        ReplaceKind.Compactor => services.ReplaceCompactor<ReplacementCompactor>(key),
        ReplaceKind.CutSelector => services.ReplaceCompactionCutSelector<ReplacementCutSelector>(key),
        ReplaceKind.Validator => services.ReplaceCompactionValidator<ReplacementCompactionValidator>(key),
        ReplaceKind.StrategyResolver => services.ReplaceCompactionStrategyResolver<ReplacementStrategyResolver>(key),
        ReplaceKind.SummaryGeneratorResolver => services.ReplaceCompactionSummaryGeneratorResolver<ReplacementSummaryGeneratorResolver>(key),
        ReplaceKind.ActivationCoordinator => services.ReplaceCompactionActivationCoordinator<ReplacementActivationCoordinator>(key),
        ReplaceKind.EventDispatcher => services.ReplaceCompactionEventDispatcher<ReplacementEventDispatcher>(key),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static object Collaborator(IServiceProvider provider, ReplaceKind kind, ComponentKey<ICompactor> key) => kind switch
    {
        ReplaceKind.Compactor => provider.GetRequiredKeyedService<ICompactor>(key.Value),
        ReplaceKind.CutSelector => provider.GetRequiredKeyedService<ICompactionCutSelector>(CompactionServiceKeys.CutSelector(key)),
        ReplaceKind.Validator => provider.GetRequiredKeyedService<ICompactionValidator>(CompactionServiceKeys.Validator(key)),
        ReplaceKind.StrategyResolver => provider.GetRequiredKeyedService<ICompactionStrategyResolver>(CompactionServiceKeys.StrategyResolver(key)),
        ReplaceKind.SummaryGeneratorResolver => provider.GetRequiredKeyedService<ICompactionSummaryGeneratorResolver>(CompactionServiceKeys.SummaryGeneratorResolver(key)),
        ReplaceKind.ActivationCoordinator => provider.GetRequiredKeyedService<ICompactionActivationCoordinator>(CompactionServiceKeys.ActivationCoordinator(key)),
        ReplaceKind.EventDispatcher => Dispatcher(provider, key),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private ServiceCollection Services()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(Coordinator);
        _ = services.AddSingleton<TimeProvider>(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UnixEpoch.AddMinutes(1)));
        return services;
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
