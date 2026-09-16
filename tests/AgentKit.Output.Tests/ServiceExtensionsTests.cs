// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

using AgentKit.Output.Tests.Fakes;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentOutput_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddAgentOutput());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentOutput_WhenProcessorKeyIsDefault_ThrowsBeforeRegistration()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(
            () => services.AddAgentOutput(default(ComponentKey<IOutputProcessor>)));

        exception.ParamName.ShouldBe("processorKey");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddAgentOutput_WhenCalled_RegistersProcessorAndResolver()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOutputProcessor>();
        _ = provider.GetRequiredService<IOutputDefinitionResolver>();
        _ = provider.GetRequiredService<IOutputSchemaEngine>();
    }

    [Fact]
    public void AddAgentOutput_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput();
        _ = services.AddAgentOutput();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IOutputProcessor>().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddAgentOutput_WhenMaximumCandidateBytesIsNotPositive_ThrowsBeforeRegistration(int value)
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddAgentOutput(options => options.MaximumCandidateBytes = value));

        exception.ParamName.ShouldBe("maximumCandidateBytes");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddOutputDefinition_RegistersDefinitionResolvableByResolver()
    {
        var services = new ServiceCollection();
        var definition = TestFactory.Definition();

        _ = services.AddAgentOutput();
        _ = services.AddOutputDefinition(definition);

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IOutputDefinitionResolver>();
        var result = await resolver.ResolveAsync(
            new OutputDefinitionRequest(definition.Id, null), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputDefinitionResolved>();
    }

    [Fact]
    public void AddOutputDefinition_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentNullException>(() => services.AddOutputDefinition(null!));
    }

    [Fact]
    public async Task AddOutputValidator_RegistersValidatorResolvableByProcessor()
    {
        var services = new ServiceCollection();
        var definition = TestFactory.Definition(validators: [new OutputValidatorReference("checker")]);

        _ = services.AddAgentOutput();
        _ = services.AddOutputValidator<AlwaysPassingValidator>();

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IOutputProcessor>();
        var result = await processor.ProcessAsync(
            new OutputProcessingRequest(definition, TestFactory.TextResponse("hi"), 1),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputAccepted>();
    }

    [Fact]
    public void ReplaceOutputSchemaEngine_WhenUsingTheDefaultProfile_ReplacesRegisteredEngine()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput();
        _ = services.ReplaceOutputSchemaEngine<PatternOutputSchemaEngine>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOutputSchemaEngine>().ShouldBeOfType<PatternOutputSchemaEngine>();
    }

    [Fact]
    public void ReplaceOutputProcessor_ReplacesRegisteredProcessor()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput();
        _ = services.ReplaceOutputProcessor<FakeOutputProcessor>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOutputProcessor>().ShouldBeOfType<FakeOutputProcessor>();
    }

    [Fact]
    public void AddAgentOutput_WhenSameKeyIsRegisteredTwice_KeepsFirstConfiguration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput(options => options.MaximumCandidateBytes = 23);
        _ = services.AddAgentOutput(options => options.MaximumCandidateBytes = 99);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<AgentOutputOptionsSnapshot>(AgentOutputDefaults.ProcessorKey.Value)
            .MaximumCandidateBytes.ShouldBe(23);
    }

    [Fact]
    public async Task AddAgentOutput_WhenTwoProfilesUseDifferentSchemaEngines_IsolatesDefinitionsAndValidation()
    {
        var structuralKey = new ComponentKey<IOutputProcessor>("structural");
        var patternKey = new ComponentKey<IOutputProcessor>("pattern");
        var definition = TestFactory.Definition(
            OutputMode.Prompted,
            schema: TestFactory.Schema(/*lang=json,strict*/"""{"pattern":"^[a-z]+$"}"""),
            retryPolicy: OutputRetryPolicy.None);
        var services = new ServiceCollection();

        _ = services.AddAgentOutput(structuralKey);
        _ = services.AddOutputDefinition(structuralKey, definition);
        _ = services.AddAgentOutput(patternKey);
        _ = services.ReplaceOutputSchemaEngine<PatternOutputSchemaEngine>(patternKey);
        _ = services.AddOutputDefinition(patternKey, definition);

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OutputDefinitionConfigurationException>(
            () => provider.GetRequiredKeyedService<IOutputDefinitionResolver>(structuralKey.Value));
        var patternResolver = provider.GetRequiredKeyedService<IOutputDefinitionResolver>(patternKey.Value);
        _ = (await patternResolver.ResolveAsync(
            new OutputDefinitionRequest(definition.Id, definition.Version),
            TestContext.Current.CancellationToken)).ShouldBeOfType<OutputDefinitionResolved>();
        await using var scope = provider.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredKeyedService<IOutputProcessor>(patternKey.Value);
        var accepted = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("\"lowercase\"")),
            TestContext.Current.CancellationToken);
        var rejected = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("\"Mixed\"")),
            TestContext.Current.CancellationToken);

        _ = accepted.ShouldBeOfType<OutputAccepted>();
        rejected.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.SchemaValidationFailed);
    }

    [Fact]
    public void AddAgentOutput_WhenDepthIsWithinSupportedRange_RegistersWithoutMutationFailure()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput(options =>
        {
            options.MaximumSchemaDepth = 128;
            options.MaximumCandidateDepth = 128;
        });

        services.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddAgentOutput_WhenDepthExceedsSupportedRange_ThrowsBeforeRegistration(bool schemaDepth)
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => services.AddAgentOutput(options =>
        {
            if (schemaDepth)
            {
                options.MaximumSchemaDepth = 129;
            }
            else
            {
                options.MaximumCandidateDepth = 129;
            }
        }));

        exception.ParamName.ShouldBe(schemaDepth ? "maximumSchemaDepth" : "maximumCandidateDepth");
        services.ShouldBeEmpty();
    }

    private sealed class AlwaysPassingValidator: IOutputValidator
    {
        public string Name => "checker";

        public ValueTask<OutputValidationResult> ValidateAsync(
            OutputValidationRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<OutputValidationResult>(OutputValidationPassed.Instance);
    }

    private sealed class FakeOutputProcessor: IOutputProcessor
    {
        public ValueTask<OutputProcessingResult> ProcessAsync(
            OutputProcessingRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
