// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using System.Text.Json;

/// <summary>Verifies the shared descriptor-identity and capability preflight every adapter runs before I/O.</summary>
public sealed class ModelRequestPreflightTests
{
    private static readonly DateTimeOffset Deadline = new(2025, 6, 1, 12, 1, 0, TimeSpan.Zero);

    private static ImmutableArray<LlmToolDefinition> Tools { get; } =
        [new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement)];

    private static LlmModelRequest CreateLlmRequest(
        ModelDescriptor descriptor,
        ImmutableArray<LlmToolDefinition> tools = default,
        LlmRequestSettings? settings = null,
        ImmutableArray<AgentMessage> messages = default) =>
        new(
            new LlmRequestContext(
                ProviderTestData.ModelRequestId,
                descriptor,
                messages.IsDefault ? [] : messages,
                tools.IsDefault ? [] : tools,
                LlmToolChoice.Auto,
                settings ?? LlmRequestSettings.Default,
                ExtensionData.Empty),
            attempt: 1,
            Deadline,
            ProviderRequestOptions.Empty);

    private static SystemMessage SystemInstruction() =>
        new(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            conversationId: null,
            new BranchId(Guid.NewGuid()),
            runId: null,
            turnId: null,
            Deadline,
            MessageState.Complete,
            [new TextPart("You are helpful.", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    private static DeveloperMessage DeveloperInstruction() =>
        new(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            conversationId: null,
            new BranchId(Guid.NewGuid()),
            runId: null,
            turnId: null,
            Deadline,
            MessageState.Complete,
            [new TextPart("Follow the style guide.", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    private static EmbeddingModelRequest CreateEmbeddingRequest(EmbeddingModelDescriptor descriptor) =>
        new(
            new EmbeddingRequestContext(
                new EmbeddingRequestId(Guid.NewGuid()),
                descriptor,
                new EmbeddingRequest(
                    [new TextEmbeddingInput("hello", null)],
                    EmbeddingPurpose.Unspecified,
                    null,
                    null,
                    EmbeddingTruncation.ProviderDefault,
                    ExtensionData.Empty)),
            attempt: 1,
            Deadline,
            ProviderRequestOptions.Empty);

    [Fact]
    public void Validate_WhenLlmRequestIsNull_ThrowsArgumentNullException()
    {
        var descriptor = ProviderTestData.Model("chat");

        var exception = Should.Throw<ArgumentNullException>(() => ModelRequestPreflight.Validate(null!, descriptor));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Validate_WhenLlmDescriptorIsNull_ThrowsArgumentNullException()
    {
        var request = CreateLlmRequest(ProviderTestData.Model("chat"));

        var exception = Should.Throw<ArgumentNullException>(() => ModelRequestPreflight.Validate(request, null!));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Validate_WhenEmbeddingRequestIsNull_ThrowsArgumentNullException()
    {
        var descriptor = ProviderTestData.EmbeddingModel("embed");

        var exception = Should.Throw<ArgumentNullException>(() => ModelRequestPreflight.Validate(null!, descriptor));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Validate_WhenEmbeddingDescriptorIsNull_ThrowsArgumentNullException()
    {
        var request = CreateEmbeddingRequest(ProviderTestData.EmbeddingModel("embed"));

        var exception = Should.Throw<ArgumentNullException>(() => ModelRequestPreflight.Validate(request, null!));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Validate_WhenLlmRequestMatchesDescriptorWithoutTools_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat");
        var request = CreateLlmRequest(descriptor);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenLlmRequestUsesStructurallyEqualDescriptorInstance_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat", toolCalls: true, parallelToolCalls: true);
        var equalCopy = ProviderTestData.Model("chat", toolCalls: true, parallelToolCalls: true);
        var request = CreateLlmRequest(equalCopy, Tools, LlmRequestSettings.Default with { ParallelToolCalls = true });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenLlmRequestModelIdDiffers_ReturnsInvalidRequestWithDescriptorMismatchMessage()
    {
        var descriptor = ProviderTestData.Model("chat");
        var request = CreateLlmRequest(descriptor with { ModelId = new ModelId("other-model") });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.ProviderId.ShouldBe(descriptor.ProviderId);
        rejected.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
        rejected.RequestId.ShouldBeNull();
        rejected.StatusCode.ShouldBeNull();
        rejected.ProviderCode.ShouldBeNull();
        rejected.RetryAfter.ShouldBeNull();
        rejected.DiagnosticCause.ShouldBeNull();
        rejected.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Validate_WhenLlmRequestCapabilitiesDiffer_ReturnsInvalidRequestWithDescriptorMismatchMessage()
    {
        var descriptor = ProviderTestData.Model("chat");
        var request = CreateLlmRequest(descriptor with
        {
            Capabilities = descriptor.Capabilities with { SupportsStructuredOutput = true },
        });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
    }

    [Fact]
    public void Validate_WhenDescriptorMismatchesAndToolsAreUnsupported_ReportsDescriptorMismatchFirst()
    {
        var descriptor = ProviderTestData.Model("chat");
        var request = CreateLlmRequest(descriptor with { ModelId = new ModelId("other-model") }, Tools);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldNotBeNull().SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
    }

    [Fact]
    public void Validate_WhenToolsRequestedAndModelDoesNotSupportToolCalls_ReturnsInvalidRequest()
    {
        var descriptor = ProviderTestData.Model("chat");
        var request = CreateLlmRequest(descriptor, Tools);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.SafeMessage.ShouldBe("The selected model does not support tool calls.");
    }

    [Fact]
    public void Validate_WhenNoToolsRequestedAndModelDoesNotSupportToolCalls_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat");
        var request = CreateLlmRequest(descriptor, settings: LlmRequestSettings.Default with { ParallelToolCalls = true });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenParallelToolCallsRequestedAndUnsupported_ReturnsInvalidRequest()
    {
        var descriptor = ProviderTestData.Model("chat", toolCalls: true);
        var request = CreateLlmRequest(descriptor, Tools, LlmRequestSettings.Default with { ParallelToolCalls = true });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.SafeMessage.ShouldBe("The selected model does not support parallel tool calls.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void Validate_WhenParallelToolCallsNotAssertedAndUnsupported_ReturnsNull(bool? parallelToolCalls)
    {
        var descriptor = ProviderTestData.Model("chat", toolCalls: true);
        var request = CreateLlmRequest(descriptor, Tools, LlmRequestSettings.Default with { ParallelToolCalls = parallelToolCalls });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenReasoningEffortRequestedAndModelDoesNotSupportReasoning_ReturnsInvalidRequest()
    {
        var descriptor = ProviderTestData.Model("chat", reasoning: false);
        var request = CreateLlmRequest(descriptor, settings: LlmRequestSettings.Default with { ReasoningEffort = LlmReasoningEffort.Medium });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.SafeMessage.ShouldBe("The selected model does not support reasoning.");
    }

    [Fact]
    public void Validate_WhenReasoningEffortNotRequestedAndModelDoesNotSupportReasoning_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat", reasoning: false);
        var request = CreateLlmRequest(descriptor);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenReasoningEffortRequestedAndModelSupportsReasoning_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat", reasoning: true);
        var request = CreateLlmRequest(descriptor, settings: LlmRequestSettings.Default with { ReasoningEffort = LlmReasoningEffort.Medium });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenSystemMessagePresentAndModelDoesNotSupportSystemInstructions_ReturnsInvalidRequest()
    {
        var descriptor = ProviderTestData.Model("chat", systemInstructions: false);
        var request = CreateLlmRequest(descriptor, messages: [SystemInstruction()]);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.SafeMessage.ShouldBe("The selected model does not support a distinct system or developer instruction role.");
    }

    [Fact]
    public void Validate_WhenDeveloperMessagePresentAndModelDoesNotSupportSystemInstructions_ReturnsInvalidRequest()
    {
        var descriptor = ProviderTestData.Model("chat", systemInstructions: false);
        var request = CreateLlmRequest(descriptor, messages: [DeveloperInstruction()]);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.SafeMessage.ShouldBe("The selected model does not support a distinct system or developer instruction role.");
    }

    [Fact]
    public void Validate_WhenNoSystemOrDeveloperMessageAndModelDoesNotSupportSystemInstructions_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat", systemInstructions: false);
        var request = CreateLlmRequest(descriptor);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenSystemMessagePresentAndModelSupportsSystemInstructions_ReturnsNull()
    {
        var descriptor = ProviderTestData.Model("chat", systemInstructions: true);
        var request = CreateLlmRequest(descriptor, messages: [SystemInstruction()]);

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenEmbeddingRequestMatchesDescriptor_ReturnsNull()
    {
        var descriptor = ProviderTestData.EmbeddingModel("embed");
        var request = CreateEmbeddingRequest(ProviderTestData.EmbeddingModel("embed"));

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenEmbeddingRequestModelIdDiffers_ReturnsInvalidRequestWithDescriptorMismatchMessage()
    {
        var descriptor = ProviderTestData.EmbeddingModel("embed");
        var request = CreateEmbeddingRequest(descriptor with { ModelId = new ModelId("other-embed") });

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        var rejected = failure.ShouldNotBeNull();
        rejected.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        rejected.ProviderId.ShouldBe(descriptor.ProviderId);
        rejected.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
        rejected.StatusCode.ShouldBeNull();
        rejected.ProviderCode.ShouldBeNull();
        rejected.DiagnosticCause.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenEmbeddingRequestCapabilitiesDiffer_ReturnsInvalidRequest()
    {
        var descriptor = ProviderTestData.EmbeddingModel("embed");
        var request = CreateEmbeddingRequest(ProviderTestData.EmbeddingModel("embed", dimensions: true));

        var failure = ModelRequestPreflight.Validate(request, descriptor);

        failure.ShouldNotBeNull().Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
    }
}
