// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Translation;

/// <summary>
/// Verifies that <see cref="AnthropicMessageTranslator"/> produces the exact
/// Anthropic Messages request body expected for representative
/// provider-neutral requests, comparing against JSON fixture files rather
/// than JSON literals embedded in test source.
/// </summary>
public sealed class AnthropicMessageTranslatorTests
{
    private static readonly AnthropicProviderOptions Options = new();

    [Fact]
    public void Translate_WhenSimpleTextConversation_MatchesExpectedRequestBody()
    {
        var messages = ImmutableArray.Create<AgentMessage>(
            TestMessages.System("You are a helpful assistant."),
            TestMessages.User("Hello!"));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            messages,
            [],
            LlmToolChoice.Auto,
            new LlmRequestSettings(
                temperature: 0.2,
                topP: null,
                maxOutputTokens: 100,
                stopSequences: [],
                parallelToolCalls: null,
                seed: null,
                ExtensionData.Empty),
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/simple_request.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenConversationHasToolUseAndResult_MatchesExpectedRequestBody()
    {
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var toolReference = new ToolReference(new ToolId("get_weather"), null, "get_weather");

        var assistantMessage = TestMessages.Assistant(
            new ToolCallPart(
                callId,
                toolReference,
                JsonDocument.Parse("""{"location":"Paris"}""").RootElement,
                new ProviderToolCallId("call_abc123"),
                ExtensionData.Empty));

        var toolMessage = TestMessages.Tool(
            new ToolResultPart(
                callId,
                toolReference,
                new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
                [new TextPart("15 degrees and sunny", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));

        var messages = ImmutableArray.Create<AgentMessage>(
            TestMessages.System("You are a weather assistant."),
            TestMessages.User("What's the weather in Paris?"),
            assistantMessage,
            toolMessage);

        var tools = ImmutableArray.Create(
            new LlmToolDefinition(
                new ToolId("get_weather"),
                "get_weather",
                "Gets the current weather for a location.",
                JsonDocument.Parse(
                    """
                    {
                      "type": "object",
                      "properties": { "location": { "type": "string" } },
                      "required": ["location"]
                    }
                    """).RootElement));

        var settings = new LlmRequestSettings(
            temperature: null,
            topP: null,
            maxOutputTokens: 200,
            stopSequences: ["STOP"],
            parallelToolCalls: false,
            seed: null,
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            messages,
            tools,
            LlmToolChoice.Required,
            settings,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: true);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/request_with_tools.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenNoMaxOutputTokensSpecifiedAnywhere_UsesOptionsDefaultFallback()
    {
        var options = new AnthropicProviderOptions { DefaultMaxOutputTokens = 777 };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new AnthropicMessageTranslator().Translate(request, options, useStreaming: false);

        actual["max_tokens"]!.GetValue<long>().ShouldBe(777);
    }

    [Fact]
    public void Translate_WhenModelLimitsSpecifyMaxOutputTokens_PrefersModelLimitOverOptionsDefault()
    {
        var descriptor = TestModels.ClaudeSonnet with { Limits = new ModelLimits(maxContextTokens: null, maxOutputTokens: 512) };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);

        actual["max_tokens"]!.GetValue<long>().ShouldBe(512);
    }

    [Fact]
    public void Translate_WhenSeedIsSpecified_ThrowsNotSupportedException()
    {
        var settings = LlmRequestSettings.Default with { Seed = 42 };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenUserMessageContainsMedia_ThrowsNotSupportedException()
    {
        var mediaPart = new MediaReferencePart(
            new MediaReference(
                new MediaId(Guid.NewGuid()),
                MediaSourceKind.Uri,
                "image/png",
                new Uri("https://example.com/image.png"),
                [],
                sizeInBytes: null,
                hash: null,
                ExtensionData.Empty),
            MediaSemantics.Input,
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User(mediaPart)],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsVisibleReasoning_TranslatesThinkingBlock()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent("internal thoughts", ReasoningVisibility.Visible, "sig-token", ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);
        var block = body["messages"]![0]!["content"]![0]!;

        block["type"]!.GetValue<string>().ShouldBe("thinking");
        block["thinking"]!.GetValue<string>().ShouldBe("internal thoughts");
        block["signature"]!.GetValue<string>().ShouldBe("sig-token");
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsRedactedReasoning_TranslatesRedactedThinkingBlock()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent(text: null, ReasoningVisibility.Redacted, "opaque-data", ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);
        var block = body["messages"]![0]!["content"]![0]!;

        block["type"]!.GetValue<string>().ShouldBe("redacted_thinking");
        block["data"]!.GetValue<string>().ShouldBe("opaque-data");
    }

    [Fact]
    public void Translate_WhenToolChoiceIsNone_SerializesNoneWithoutDisableParallelFlag()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = false };

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.None,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);

        body["tool_choice"]!["type"]!.GetValue<string>().ShouldBe("none");
        body["tool_choice"]!.AsObject().ContainsKey("disable_parallel_tool_use").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenNamedToolChoice_SerializesToolTypeWithName()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.Named("noop"),
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);

        body["tool_choice"]!["type"]!.GetValue<string>().ShouldBe("tool");
        body["tool_choice"]!["name"]!.GetValue<string>().ShouldBe("noop");
    }

    [Fact]
    public void Translate_WhenMultipleSystemAndDeveloperMessages_ConcatenatesIntoOneSystemString()
    {
        var developer = new DeveloperMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            conversationId: null,
            new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
            runId: null,
            turnId: null,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            MessageState.Complete,
            [new TextPart("Follow the house style.", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.System("You are helpful."), developer, TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);

        body["system"]!.GetValue<string>().ShouldBe("You are helpful.\n\nFollow the house style.");
        body["messages"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public void Translate_WhenExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedModel = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("hacked-model")]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("model", hackedModel));
        var options = new ProviderRequestOptions(extensions);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);

        body["model"]!.GetValue<string>().ShouldBe(TestModels.ClaudeSonnet.ModelId.Value);
    }

    [Fact]
    public void Translate_WhenMessageIsRuntimeMessage_ProjectsTheSharedTaggedEnvelope()
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.Runtime("The run was interrupted.")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AnthropicMessageTranslator().Translate(request, Options, useStreaming: false);
        var message = body["messages"]![0]!.AsObject();

        message["role"]!.GetValue<string>().ShouldBe("user");
        var block = message["content"]![0]!.AsObject();
        block["type"]!.GetValue<string>().ShouldBe("text");
        var notice = JsonNode.Parse(block["text"]!.GetValue<string>())!;
        notice["format"]!.GetValue<string>().ShouldBe(RuntimeMessageProjection.Format);
        notice["content"]!.GetValue<string>().ShouldBe("The run was interrupted.");

        // The envelope tags the notice so it is never indistinguishable from a plain user message.
        block["text"]!.GetValue<string>().ShouldNotBe("The run was interrupted.");
    }
}
