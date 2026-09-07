// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentOutput_WhenCalled_RegistersProcessorAndResolver()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOutputProcessor>();
        _ = provider.GetRequiredService<IOutputDefinitionResolver>();
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
    public void AddAgentOutput_WhenMaximumCandidateBytesIsNotPositive_FailsValidationOnAccess(int value)
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput(options => options.MaximumCandidateBytes = value);

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IOutputProcessor>);
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
    public void ReplaceOutputProcessor_ReplacesRegisteredProcessor()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentOutput();
        _ = services.ReplaceOutputProcessor<FakeOutputProcessor>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOutputProcessor>().ShouldBeOfType<FakeOutputProcessor>();
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
