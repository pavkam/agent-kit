// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests.Translation;

/// <summary>
/// Verifies that <see cref="CohereRequestTranslator"/> produces the exact
/// Cohere v2 Chat request body expected for representative
/// provider-neutral requests, comparing against JSON fixture files rather
/// than JSON literals embedded in test source.
/// </summary>
public sealed class CohereRequestTranslatorTests
{
    [Fact]
    public void Translate_WhenSimpleTextConversation_MatchesExpectedRequestBody()
    {
        var messages = ImmutableArray.Create<AgentMessage>(
            TestMessages.System("You are a helpful assistant."),
            TestMessages.User("Hello!"));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
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

        var actual = new CohereRequestTranslator().Translate(request, useStreaming: false);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/simple_request.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenConversationHasToolUseAndResult_MatchesExpectedRequestBody()
    {
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var toolReference = new ToolReference(new ToolAlias("get_weather"), null, null);

        var assistantMessage = TestMessages.Assistant(
            new ToolCallPart(
                callId,
                toolReference,
                JsonDocument.Parse("""{"location":"Paris"}""").RootElement,
                new ProviderToolCallId("call_abc123"),
                ExtensionData.Empty));

        var toolMessage = TestMessages.Tool(
            new ToolResultPart(callId, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart("15 degrees and sunny", TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty));

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
            parallelToolCalls: null,
            seed: null,
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            messages,
            tools,
            LlmToolChoice.Required,
            settings,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new CohereRequestTranslator().Translate(request, useStreaming: false);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/request_with_tools.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenUseStreamingIsTrue_SetsStreamFieldToTrue()
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: true);

        body["stream"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Translate_WhenTopPIsSpecified_SetsPFieldNotTopP()
    {
        var settings = LlmRequestSettings.Default with { TopP = 0.9 };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body["p"]!.GetValue<double>().ShouldBe(0.9);
        body.AsObject().ContainsKey("top_p").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenSeedIsSpecified_SetsSeedField()
    {
        var settings = LlmRequestSettings.Default with { Seed = 42 };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body["seed"]!.GetValue<long>().ShouldBe(42);
    }

    [Fact]
    public void Translate_WhenParallelToolCallsIsTrue_DoesNotThrowAndOmitsParallelControl()
    {
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = true };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body.AsObject().ContainsKey("parallel_tool_calls").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenParallelToolCallsIsFalse_ThrowsNotSupportedException()
    {
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = false };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new CohereRequestTranslator().Translate(request, useStreaming: false));
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
            TestModels.CommandAPlus,
            [TestMessages.User(mediaPart)],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new CohereRequestTranslator().Translate(request, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsVisibleReasoning_TranslatesThinkingBlock()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent("internal thoughts", ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);
        var block = body["messages"]![0]!["content"]![0]!;

        block["type"]!.GetValue<string>().ShouldBe("thinking");
        block["thinking"]!.GetValue<string>().ShouldBe("internal thoughts");
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsRedactedReasoning_ThrowsNotSupportedException()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent(text: null, ReasoningVisibility.Redacted, signatureToken: "opaque", ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new CohereRequestTranslator().Translate(request, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenToolChoiceIsAuto_OmitsToolChoiceField()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body.AsObject().ContainsKey("tool_choice").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenToolChoiceIsNone_SerializesNoneString()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.None,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body["tool_choice"]!.GetValue<string>().ShouldBe("NONE");
    }

    [Fact]
    public void Translate_WhenNamedToolChoice_ThrowsNotSupportedException()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.Named("noop"),
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new CohereRequestTranslator().Translate(request, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenToolMessageHasMultipleResults_ExpandsIntoSeparateToolMessages()
    {
        var callIdA = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000003"));
        var callIdB = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000004"));
        var toolReference = new ToolReference(new ToolAlias("noop"), null, null);

        var toolMessage = TestMessages.Tool(
            new ToolResultPart(callIdA, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart("result a", TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty),
            new ToolResultPart(callIdB, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, "boom", ExtensionData.Empty), [], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [toolMessage],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);
        var messages = body["messages"]!.AsArray();

        messages.Count.ShouldBe(2);
        messages[0]!["content"]!.GetValue<string>().ShouldBe("result a");
        messages[0]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdA.ToString());
        messages[0]!.AsObject().ContainsKey("name").ShouldBeFalse();
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
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body["model"]!.GetValue<string>().ShouldBe(TestModels.CommandAPlus.ModelId.Value);
    }

    [Fact]
    public void Translate_WhenProviderRequestOptionsCarryExtensionData_AddsThemToBody()
    {
        var value = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("CONTEXTUAL")]);
        var options = new ProviderRequestOptions(
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("safety_mode", value)));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);

        body["safety_mode"]!.GetValue<string>().ShouldBe("CONTEXTUAL");
    }

    [Fact]
    public void Translate_WhenMessageIsRuntimeMessage_ProjectsTheSharedTaggedEnvelope()
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.CommandAPlus,
            [TestMessages.Runtime("The run was interrupted.")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new CohereRequestTranslator().Translate(request, useStreaming: false);
        var message = body["messages"]![0]!.AsObject();

        message["role"]!.GetValue<string>().ShouldBe("user");
        var notice = JsonNode.Parse(message["content"]!.GetValue<string>())!;
        notice["format"]!.GetValue<string>().ShouldBe(RuntimeMessageProjection.Format);
        notice["content"]!.GetValue<string>().ShouldBe("The run was interrupted.");

        // The envelope tags the notice so it is never indistinguishable from a plain user message.
        message["content"]!.GetValue<string>().ShouldNotBe("The run was interrupted.");
    }
}
