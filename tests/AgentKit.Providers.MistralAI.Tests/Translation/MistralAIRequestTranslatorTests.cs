// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests.Translation;

/// <summary>
/// Verifies that <see cref="MistralAIRequestTranslator"/> produces the
/// exact Mistral AI Chat Completions request body expected for
/// representative provider-neutral requests, comparing against JSON
/// fixture files rather than JSON literals embedded in test source.
/// </summary>
public sealed class MistralAIRequestTranslatorTests
{
    [Fact]
    public void Translate_WhenSimpleTextConversation_MatchesExpectedRequestBody()
    {
        var messages = ImmutableArray.Create<AgentMessage>(
            TestMessages.System("You are a helpful assistant."),
            TestMessages.User("Hello!"));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
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

        var actual = new MistralAIRequestTranslator().Translate(request, useStreaming: false);
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
            TestModels.MistralLarge,
            messages,
            tools,
            LlmToolChoice.Required,
            settings,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new MistralAIRequestTranslator().Translate(request, useStreaming: false);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/request_with_tools.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenUseStreamingIsTrue_SetsStreamFieldToTrue()
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: true);

        body["stream"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Translate_WhenSeedIsSpecified_SetsRandomSeedField()
    {
        var settings = LlmRequestSettings.Default with { Seed = 42 };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);

        body["random_seed"]!.GetValue<long>().ShouldBe(42);
        body.AsObject().ContainsKey("seed").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenParallelToolCallsIsSpecified_SetsParallelToolCallsField()
    {
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = true };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);

        body["parallel_tool_calls"]!.GetValue<bool>().ShouldBeTrue();
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
            TestModels.MistralLarge,
            [TestMessages.User(mediaPart)],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new MistralAIRequestTranslator().Translate(request, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsReasoning_ThrowsNotSupportedException()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent("internal thoughts", ReasoningVisibility.Visible, "sig-token", ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new MistralAIRequestTranslator().Translate(request, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenToolChoiceIsNone_SerializesNoneString()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.None,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);

        body["tool_choice"]!.GetValue<string>().ShouldBe("none");
    }

    [Fact]
    public void Translate_WhenNamedToolChoice_SerializesFunctionObject()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.Named("noop"),
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);

        body["tool_choice"]!["type"]!.GetValue<string>().ShouldBe("function");
        body["tool_choice"]!["function"]!["name"]!.GetValue<string>().ShouldBe("noop");
    }

    [Fact]
    public void Translate_WhenMultipleSystemMessages_KeepsThemAsSeparateMessages()
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
            TestModels.MistralLarge,
            [TestMessages.System("You are helpful."), developer, TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);
        var messages = body["messages"]!.AsArray();

        messages.Count.ShouldBe(3);
        messages[0]!["role"]!.GetValue<string>().ShouldBe("system");
        messages[0]!["content"]!.GetValue<string>().ShouldBe("You are helpful.");
        messages[1]!["role"]!.GetValue<string>().ShouldBe("system");
        messages[1]!["content"]!.GetValue<string>().ShouldBe("Follow the house style.");
    }

    [Fact]
    public void Translate_WhenToolMessageHasMultipleResults_ExpandsIntoSeparateToolMessages()
    {
        var callIdA = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000003"));
        var callIdB = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000004"));
        var toolReference = new ToolReference(new ToolId("noop"), null, "noop");

        var toolMessage = TestMessages.Tool(
            new ToolResultPart(
                callIdA,
                toolReference,
                new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
                [new TextPart("result a", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
            new ToolResultPart(
                callIdB,
                toolReference,
                new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, "boom", ExtensionData.Empty),
                [],
                ExtensionData.Empty));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [toolMessage],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);
        var messages = body["messages"]!.AsArray();

        messages.Count.ShouldBe(2);
        messages[0]!["content"]!.GetValue<string>().ShouldBe("result a");
        messages[0]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdA.ToString());
        messages[1]!["content"]!.GetValue<string>().ShouldBe("Error: boom");
        messages[1]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdB.ToString());
    }

    [Fact]
    public void Translate_WhenExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedModel = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("hacked-model")]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("model", hackedModel));
        var options = new ProviderRequestOptions(extensions);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);

        body["model"]!.GetValue<string>().ShouldBe(TestModels.MistralLarge.ModelId.Value);
    }

    [Fact]
    public void Translate_WhenProviderRequestOptionsCarryExtensionData_AddsThemToBody()
    {
        var value = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-42")]);
        var options = new ProviderRequestOptions(
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("prompt_cache_key", value)));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new MistralAIRequestTranslator().Translate(request, useStreaming: false);

        body["prompt_cache_key"]!.GetValue<string>().ShouldBe("end-user-42");
    }
}
