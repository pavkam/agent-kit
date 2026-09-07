// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Translation;

/// <summary>
/// Verifies that <see cref="OpenAIRequestTranslator"/> produces the exact
/// OpenAI-compatible request body expected for representative
/// provider-neutral requests, comparing against JSON fixture files rather
/// than JSON literals embedded in test source.
/// </summary>
public sealed class OpenAIRequestTranslatorTests
{
    private static readonly OpenAICompatibilityProfile Profile = new(
        new Uri("https://api.openai.com/"),
        "v1/chat/completions",
        sendDeveloperRoleAsSystem: false,
        preferStreaming: false,
        includeStreamUsage: true,
        useMaxCompletionTokensField: true,
        []);

    [Fact]
    public void Translate_WhenSimpleTextConversation_MatchesExpectedRequestBody()
    {
        var messages = ImmutableArray.Create<AgentMessage>(
            TestMessages.System("You are a helpful assistant."),
            TestMessages.User("Hello!"));

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
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

        var actual = new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: false);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/simple_chat_request.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenConversationHasToolCallAndResult_MatchesExpectedRequestBody()
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
                new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
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
            maxOutputTokens: null,
            stopSequences: ["STOP"],
            parallelToolCalls: true,
            seed: 42,
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
            messages,
            tools,
            LlmToolChoice.Required,
            settings,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: true);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/chat_request_with_tools.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
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

        var message = TestMessages.User(mediaPart);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
            [message],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        _ = Should.Throw<NotSupportedException>(() => new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenIncompleteMessagePresent_OmitsItFromHistory()
    {
        var incomplete = TestMessages.User("Draft, still streaming...", state: MessageState.Incomplete);
        var complete = TestMessages.User("Final question.");

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
            [incomplete, complete],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);

        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);

        var actual = new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: false);
        var messages = actual["messages"]!.AsArray();

        messages.Count.ShouldBe(1);
        messages[0]!["content"]!.GetValue<string>().ShouldBe("Final question.");
    }
}
