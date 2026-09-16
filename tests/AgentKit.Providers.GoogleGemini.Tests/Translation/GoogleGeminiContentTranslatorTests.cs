// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests.Translation;

using AgentKit.Providers.GoogleGemini.Tests.Fakes;

/// <summary>
/// Verifies that <see cref="GoogleGeminiContentTranslator"/> produces the
/// exact Gemini GenerateContent request body expected for representative
/// provider-neutral requests, comparing against JSON fixture files rather
/// than JSON literals embedded in test source.
/// </summary>
public sealed class GoogleGeminiContentTranslatorTests
{
    [Fact]
    public void Translate_WhenSimpleTextConversation_MatchesExpectedRequestBody()
    {
        var messages = ImmutableArray.Create<AgentMessage>(
            TestMessages.System("You are a helpful assistant."),
            TestMessages.User("Hello!"));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
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

        var actual = new GoogleGeminiContentTranslator().Translate(request);
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
            parallelToolCalls: null,
            seed: null,
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            messages,
            tools,
            LlmToolChoice.Required,
            settings,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new GoogleGeminiContentTranslator().Translate(request);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/request_with_tools.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenParallelToolCallsIsTrue_DoesNotThrowAndOmitsParallelControl()
    {
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = true };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body.AsObject().ContainsKey("generationConfig").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenParallelToolCallsIsFalse_ThrowsNotSupportedException()
    {
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = false };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new GoogleGeminiContentTranslator().Translate(request));
    }

    [Fact]
    public void Translate_WhenSeedIsSpecified_IncludesSeedInGenerationConfig()
    {
        var settings = LlmRequestSettings.Default with { Seed = 42 };
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            settings,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["generationConfig"]!["seed"]!.GetValue<long>().ShouldBe(42);
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
            TestModels.GeminiFlash,
            [TestMessages.User(mediaPart)],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new GoogleGeminiContentTranslator().Translate(request));
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsVisibleReasoning_TranslatesThoughtPart()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent("internal thoughts", ReasoningVisibility.Visible, "sig-token", ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);
        var part = body["contents"]![0]!["parts"]![0]!;

        part["text"]!.GetValue<string>().ShouldBe("internal thoughts");
        part["thought"]!.GetValue<bool>().ShouldBeTrue();
        part["thoughtSignature"]!.GetValue<string>().ShouldBe("sig-token");
    }

    /// <summary>Verifies a retained function-call thoughtSignature is re-emitted as a sibling of functionCall on the same wire part.</summary>
    [Fact]
    public void Translate_WhenToolCallPartCarriesThoughtSignature_EmitsThoughtSignatureOnFunctionCallPart()
    {
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000003"));
        var toolReference = new ToolReference(new ToolId("check_flight"), null, "check_flight");
        var signedCall = new ToolCallPart(
            callId,
            toolReference,
            JsonDocument.Parse("""{"flight":"AA100"}""").RootElement,
            new ProviderToolCallId("call_sig_001"),
            GoogleGeminiThoughtSignature.Create("sig_fc_alpha"));
        var unsignedCall = new ToolCallPart(
            new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000004")),
            new ToolReference(new ToolId("book_taxi"), null, "book_taxi"),
            JsonDocument.Parse("""{"time":"18:00"}""").RootElement,
            new ProviderToolCallId("call_sig_002"),
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("Check AA100."), TestMessages.Assistant(signedCall, unsignedCall)],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);
        var parts = body["contents"]![1]!["parts"]!.AsArray();

        var expectedSigned = JsonNode.Parse(
            /*lang=json,strict*/ """{"functionCall":{"id":"call_sig_001","name":"check_flight","args":{"flight":"AA100"}},"thoughtSignature":"sig_fc_alpha"}""");
        JsonNode.DeepEquals(parts[0], expectedSigned).ShouldBeTrue(parts[0]!.ToJsonString());
        parts[1]!.AsObject().ContainsKey("thoughtSignature").ShouldBeFalse(parts[1]!.ToJsonString());
        parts[1]!["functionCall"]!.AsObject().ContainsKey("thoughtSignature").ShouldBeFalse();
    }

    /// <summary>Verifies a retained answer-text thoughtSignature is re-emitted on the same text wire part.</summary>
    [Fact]
    public void Translate_WhenTextPartCarriesThoughtSignature_EmitsThoughtSignatureOnTextPart()
    {
        var assistant = TestMessages.Assistant(
            new TextPart("Flight AA100 is on time.", TextSemantics.Plain, GoogleGeminiThoughtSignature.Create("sig_text_omega")));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);
        var part = body["contents"]![0]!["parts"]![0]!;

        var expected = JsonNode.Parse(/*lang=json,strict*/ """{"text":"Flight AA100 is on time.","thoughtSignature":"sig_text_omega"}""");
        JsonNode.DeepEquals(part, expected).ShouldBeTrue(part.ToJsonString());
    }

    /// <summary>Verifies a user-authored text part never gains a thoughtSignature field, even if it carries the extension.</summary>
    [Fact]
    public void Translate_WhenUserTextPartCarriesThoughtSignatureExtension_DoesNotEmitThoughtSignature()
    {
        var user = TestMessages.User(new TextPart("hi", TextSemantics.Plain, GoogleGeminiThoughtSignature.Create("sig_not_model")));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [user],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["contents"]![0]!["parts"]![0]!.AsObject().ContainsKey("thoughtSignature").ShouldBeFalse();
    }

    /// <summary>Verifies a parsed response with signed function-call, reasoning, and text parts translates back with every signature in place.</summary>
    [Fact]
    public async Task Translate_WhenAssistantPartsComeFromParsedResponses_RoundTripsEveryThoughtSignatureInPlace()
    {
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());
        var parseContext = new ProviderResponseParseContext(
            new ModelRequestId(Guid.NewGuid()),
            GoogleGeminiProviderDefaults.ProviderId,
            GoogleGeminiProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId: null,
            providerRequestId: null);
        var parts = new List<ContentPart>();
        foreach (var fixture in new[] { "responses/buffered_tool_use_with_signature.json", "responses/buffered_thinking.json", "responses/buffered_text_with_signature.json" })
        {
            await using var body = File.OpenRead(TestResources.GetPath(fixture));
            var completed = (await parser.ParseBufferedAsync(body, parseContext, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken))
                .ShouldBeOfType<ModelAttemptCompleted>();
            parts.AddRange(completed.Response.Parts);
        }

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.Assistant([.. parts])],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var wireParts = new GoogleGeminiContentTranslator().Translate(request)["contents"]![0]!["parts"]!.AsArray();

        wireParts.Count.ShouldBe(5);
        wireParts[0]!["functionCall"]!["name"]!.GetValue<string>().ShouldBe("check_flight");
        wireParts[0]!["thoughtSignature"]!.GetValue<string>().ShouldBe("sig_fc_alpha");
        wireParts[1]!["functionCall"]!["name"]!.GetValue<string>().ShouldBe("book_taxi");
        wireParts[1]!.AsObject().ContainsKey("thoughtSignature").ShouldBeFalse();
        wireParts[2]!["thought"]!.GetValue<bool>().ShouldBeTrue();
        wireParts[2]!["thoughtSignature"]!.GetValue<string>().ShouldBe("sig_abc123");
        wireParts[3]!["text"]!.GetValue<string>().ShouldBe("The answer is 42.");
        wireParts[3]!.AsObject().ContainsKey("thoughtSignature").ShouldBeFalse();
        wireParts[4]!["text"]!.GetValue<string>().ShouldBe("Flight AA100 is on time.");
        wireParts[4]!["thoughtSignature"]!.GetValue<string>().ShouldBe("sig_text_omega");
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsRedactedReasoning_ThrowsNotSupportedException()
    {
        var reasoning = new ReasoningPart(
            new ReasoningContent(text: null, ReasoningVisibility.Redacted, "opaque-data", ExtensionData.Empty),
            ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new GoogleGeminiContentTranslator().Translate(request));
    }

    [Fact]
    public void Translate_WhenToolChoiceIsNone_SerializesNoneMode()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.None,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["toolConfig"]!["functionCallingConfig"]!["mode"]!.GetValue<string>().ShouldBe("NONE");
    }

    [Fact]
    public void Translate_WhenNamedToolChoice_SerializesAnyModeWithAllowedFunctionNames()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.Named("noop"),
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["toolConfig"]!["functionCallingConfig"]!["mode"]!.GetValue<string>().ShouldBe("ANY");
        body["toolConfig"]!["functionCallingConfig"]!["allowedFunctionNames"]![0]!.GetValue<string>().ShouldBe("noop");
    }

    [Fact]
    public void Translate_WhenMultipleSystemAndDeveloperMessages_ConcatenatesIntoOneSystemInstruction()
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
            TestModels.GeminiFlash,
            [TestMessages.System("You are helpful."), developer, TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>()
            .ShouldBe("You are helpful.\n\nFollow the house style.");
        body["contents"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public void Translate_WhenExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedContents = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("hacked")]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("contents", hackedContents));
        var options = new ProviderRequestOptions(extensions);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["contents"]!.AsArray().Count.ShouldBe(1);
        body["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>().ShouldBe("hi");
    }

    [Fact]
    public void Translate_WhenProviderRequestOptionsCarryExtensionData_AddsThemToBody()
    {
        var value = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-42")]);
        var options = new ProviderRequestOptions(
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("cachedContent", value)));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options);

        var body = new GoogleGeminiContentTranslator().Translate(request);

        body["cachedContent"]!.GetValue<string>().ShouldBe("end-user-42");
    }

    [Fact]
    public void Translate_WhenToolResultOutcomeIsFailure_TranslatesErrorField()
    {
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var toolReference = new ToolReference(new ToolId("get_weather"), null, "get_weather");

        var toolMessage = TestMessages.Tool(
            new ToolResultPart(
                callId,
                toolReference,
                new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, "The location was not found.", ExtensionData.Empty),
                [],
                ExtensionData.Empty));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [toolMessage],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);
        var functionResponse = body["contents"]![0]!["parts"]![0]!["functionResponse"]!;

        functionResponse["response"]!["error"]!.GetValue<string>().ShouldBe("The location was not found.");
    }

    [Fact]
    public void Translate_WhenMessageIsRuntimeMessage_ProjectsTheSharedTaggedEnvelope()
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.GeminiFlash,
            [TestMessages.Runtime("The run was interrupted.")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new GoogleGeminiContentTranslator().Translate(request);
        var content = body["contents"]![0]!.AsObject();

        content["role"]!.GetValue<string>().ShouldBe("user");
        var part = content["parts"]![0]!.AsObject();
        var notice = JsonNode.Parse(part["text"]!.GetValue<string>())!;
        notice["format"]!.GetValue<string>().ShouldBe(RuntimeMessageProjection.Format);
        notice["content"]!.GetValue<string>().ShouldBe("The run was interrupted.");

        // The envelope tags the notice so it is never indistinguishable from a plain user message.
        part["text"]!.GetValue<string>().ShouldNotBe("The run was interrupted.");
    }
}
