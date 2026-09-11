// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies LlmRequestContext behavior and contracts.</summary>
public sealed class LlmRequestContextTests
{
    private static LlmModelRequest CreateRequest()
    {
        var capabilities = new ModelCapabilities(supportsSystemInstructions: true, supportsStreaming: true, supportsToolCalls: true, supportsParallelToolCalls: true, supportsStructuredOutput: true, supportsReasoning: true, supportsVisionInput: true, ExtensionData.Empty);
        var model = new ModelDescriptor(new ModelAlias("chat"), new ProviderId("test-provider"), new ApiFamilyId("test-api"), new ModelId("test-model"), deploymentId: null, capabilities, new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024), pricing: null, ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(new Guid("dc591d0d-5ae3-47ee-8255-e1357764fc0e")), model, messages: [], tools: [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, DateTimeOffset.UnixEpoch.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    [Fact]
    public void LlmRequestContext_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = CreateRequest().Context;
        var second = CreateRequest().Context;
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void WithExpression_WhenModelIsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentNullException>(() => _ = context with { Model = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenMessagesIsDefault_ThrowsArgumentException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentException>(() => _ = context with { Messages = default });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenToolsIsDefault_ThrowsArgumentException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentException>(() => _ = context with { Tools = default });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenToolChoiceIsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentNullException>(() => _ = context with { ToolChoice = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenSettingsIsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentNullException>(() => _ = context with { Settings = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenContextExtensionsIsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentNullException>(() => _ = context with { Extensions = null! });
        exception.ParamName.ShouldBe("value");
    }
}
