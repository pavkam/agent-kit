// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Translation;

/// <summary>
/// Verifies that <see cref="AwsBedrockRequestTranslator"/> produces the
/// exact Bedrock Converse request body expected for representative
/// provider-neutral requests, comparing against JSON fixture files rather
/// than JSON literals embedded in test source.
/// </summary>
public sealed class AwsBedrockRequestTranslatorTests
{
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

        var actual = new AwsBedrockRequestTranslator().Translate(request);
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

        var actual = new AwsBedrockRequestTranslator().Translate(request);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/request_with_tools.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenNoMaxOutputTokensSpecified_OmitsMaxTokensField()
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new AwsBedrockRequestTranslator().Translate(request);

        actual.ContainsKey("inferenceConfig").ShouldBeFalse();
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

        _ = Should.Throw<NotSupportedException>(() => new AwsBedrockRequestTranslator().Translate(request));
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

        _ = Should.Throw<NotSupportedException>(() => new AwsBedrockRequestTranslator().Translate(request));
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
            TestModels.ClaudeSonnet,
            [assistant],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new AwsBedrockRequestTranslator().Translate(request));
    }

    [Fact]
    public void Translate_WhenToolChoiceIsAuto_SerializesAutoMarker()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AwsBedrockRequestTranslator().Translate(request);

        body["toolConfig"]!["toolChoice"]!.AsObject().ContainsKey("auto").ShouldBeTrue();
    }

    [Fact]
    public void Translate_WhenToolChoiceIsNone_OmitsToolChoiceField()
    {
        var tool = new LlmToolDefinition(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi")],
            [tool],
            LlmToolChoice.None,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AwsBedrockRequestTranslator().Translate(request);

        body["toolConfig"]!.AsObject().ContainsKey("toolChoice").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenNamedToolChoice_SerializesToolMarkerWithName()
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

        var body = new AwsBedrockRequestTranslator().Translate(request);

        body["toolConfig"]!["toolChoice"]!["tool"]!["name"]!.GetValue<string>().ShouldBe("noop");
    }

    [Fact]
    public void Translate_WhenMultipleSystemAndDeveloperMessages_AppendsEachAsSeparateSystemBlock()
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

        var body = new AwsBedrockRequestTranslator().Translate(request);

        body["system"]!.AsArray().Count.ShouldBe(2);
        body["system"]![0]!["text"]!.GetValue<string>().ShouldBe("You are helpful.");
        body["system"]![1]!["text"]!.GetValue<string>().ShouldBe("Follow the house style.");
        body["messages"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public void Translate_WhenExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedMessages = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(Array.Empty<object>())]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("messages", hackedMessages));
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

        var body = new AwsBedrockRequestTranslator().Translate(request);

        body["messages"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public void Translate_WhenToolResultOutcomeIsFailure_SerializesErrorStatus()
    {
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var toolReference = new ToolReference(new ToolId("get_weather"), null, "get_weather");

        var toolMessage = TestMessages.Tool(
            new ToolResultPart(
                callId,
                toolReference,
                new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, "boom", ExtensionData.Empty),
                [new TextPart("failed", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.ClaudeSonnet,
            [TestMessages.User("hi"), toolMessage],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var body = new AwsBedrockRequestTranslator().Translate(request);
        var block = body["messages"]![1]!["content"]![0]!["toolResult"]!;

        block["status"]!.GetValue<string>().ShouldBe("error");
    }
}
