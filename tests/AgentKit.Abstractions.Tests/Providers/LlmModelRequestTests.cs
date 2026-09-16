// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies LlmModelRequest behavior and contracts.</summary>
public sealed class LlmModelRequestTests
{
    private static LlmModelRequest CreateRequest()
    {
        var capabilities = new ModelCapabilities(supportsSystemInstructions: true, supportsStreaming: true, supportsToolCalls: true, supportsParallelToolCalls: true, supportsStructuredOutput: true, supportsReasoning: true, supportsVisionInput: true, ExtensionData.Empty);
        var model = new ModelDescriptor(new ModelAlias("chat"), new ProviderId("test-provider"), new ApiFamilyId("test-api"), new ModelId("test-model"), deploymentId: null, capabilities, new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024), pricing: null, ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(new Guid("dc591d0d-5ae3-47ee-8255-e1357764fc0e")), model, messages: [], tools: [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, DateTimeOffset.UnixEpoch.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    [Fact]
    public void WithExpression_WhenContextIsNull_ThrowsArgumentNullException()
    {
        var request = CreateRequest();
        var exception = Should.Throw<ArgumentNullException>(() => _ = request with { Context = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithExpression_WhenAttemptIsNotPositive_ThrowsArgumentOutOfRangeException(int attempt)
    {
        var request = CreateRequest();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = request with { Attempt = attempt });
        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(attempt);
    }

    [Fact]
    public void WithExpression_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var request = CreateRequest();
        var exception = Should.Throw<ArgumentNullException>(() => _ = request with { Options = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenChangesAreValid_PreservesConfigurabilityAndOriginalValues()
    {
        var original = CreateRequest();
        var updatedSettings = original.Context.Settings with
        {
            Temperature = 0.25,
            TopP = 0.9,
            MaxOutputTokens = 0,
            StopSequences = ["done"],
            ParallelToolCalls = false,
            Seed = 42,
        };
        var updatedContext = original.Context with
        {
            Messages = [],
            Tools = [],
            Settings = updatedSettings,
        };
        var updated = original with
        {
            Context = updatedContext,
            Attempt = 2,
            Deadline = original.Deadline.AddMinutes(1),
            Options = new ProviderRequestOptions(ExtensionData.Empty),
        };
        updated.Attempt.ShouldBe(2);
        updated.Context.Settings.MaxOutputTokens.ShouldBe(0);
        updated.Context.Settings.StopSequences.ShouldBe(["done"]);
        updated.Context.Settings.Temperature.ShouldBe(0.25);
        original.Attempt.ShouldBe(1);
        original.Context.Settings.ShouldBeSameAs(LlmRequestSettings.Default);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsOptions()
    {
        var request = CreateRequest();
        request.Options.ShouldBe(ProviderRequestOptions.Empty);
    }
}
