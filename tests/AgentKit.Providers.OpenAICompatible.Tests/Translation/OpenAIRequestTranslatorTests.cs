// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Translation;



/// <summary>Verifies OpenAIRequestTranslator behavior and contracts.</summary>
public sealed class OpenAIRequestTranslatorTests
{
    private static readonly OpenAICompatibilityProfile Profile = new(new Uri("https://api.openai.com/"), "v1/chat/completions", sendDeveloperRoleAsSystem: false, preferStreaming: false, includeStreamUsage: true, useMaxCompletionTokensField: true, []);
    [Fact]
    public void Translate_WhenSimpleTextConversation_MatchesExpectedRequestBody()
    {
        var messages = ImmutableArray.Create<AgentMessage>(TestMessages.System("You are a helpful assistant."), TestMessages.User("Hello!"));
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), TestModels.Gpt4O, messages, [], LlmToolChoice.Auto, new LlmRequestSettings(temperature: 0.2, topP: null, maxOutputTokens: 100, stopSequences: [], parallelToolCalls: null, seed: null, ExtensionData.Empty), ExtensionData.Empty);
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
        var assistantMessage = TestMessages.Assistant(new ToolCallPart(callId, toolReference, JsonDocument.Parse("""{"location":"Paris"}""").RootElement, new ProviderToolCallId("call_abc123"), ExtensionData.Empty));
        var toolMessage = TestMessages.Tool(new ToolResultPart(callId, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty), [new TextPart("15 degrees and sunny", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty));
        var messages = ImmutableArray.Create<AgentMessage>(TestMessages.System("You are a weather assistant."), TestMessages.User("What's the weather in Paris?"), assistantMessage, toolMessage);
        var tools = ImmutableArray.Create(new LlmToolDefinition(new ToolId("get_weather"), "get_weather", "Gets the current weather for a location.", JsonDocument.Parse("""
                    {
                      "type": "object",
                      "properties": { "location": { "type": "string" } },
                      "required": ["location"]
                    }
                    """).RootElement));
        var settings = new LlmRequestSettings(temperature: null, topP: null, maxOutputTokens: null, stopSequences: ["STOP"], parallelToolCalls: true, seed: 42, ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), TestModels.Gpt4O, messages, tools, LlmToolChoice.Required, settings, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);
        var actual = new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: true);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/chat_request_with_tools.json"));
        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenUserMessageContainsMedia_ThrowsNotSupportedException()
    {
        var mediaPart = new MediaReferencePart(new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.Uri, "image/png", new Uri("https://example.com/image.png"), [], sizeInBytes: null, hash: null, ExtensionData.Empty), MediaSemantics.Input, ExtensionData.Empty);
        var message = TestMessages.User(mediaPart);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), TestModels.Gpt4O, [message], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);
        _ = Should.Throw<NotSupportedException>(() => new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: false));
    }

    [Fact]
    public void Translate_WhenIncompleteMessagePresent_OmitsItFromHistory()
    {
        var incomplete = TestMessages.User("Draft, still streaming...", state: MessageState.Incomplete);
        var complete = TestMessages.User("Final question.");
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), TestModels.Gpt4O, [incomplete, complete], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);
        var actual = new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: false);
        var messages = actual["messages"]!.AsArray();
        messages.Count.ShouldBe(1);
        messages[0]!["content"]!.GetValue<string>().ShouldBe("Final question.");
    }

    private static readonly OpenAICompatibilityProfile NonStreamingProfile = new(new Uri("https://api.openai.com/"), "v1/chat/completions", sendDeveloperRoleAsSystem: false, preferStreaming: false, includeStreamUsage: true, useMaxCompletionTokensField: true, []);
    private static readonly LlmToolDefinition SampleTool = new(new ToolId("noop"), "noop", null, JsonDocument.Parse("{}").RootElement);
    private static JsonObject Translate(ImmutableArray<AgentMessage> messages, LlmToolChoice? toolChoice = null, ImmutableArray<LlmToolDefinition> tools = default, LlmRequestSettings? settings = null, ProviderRequestOptions? options = null, OpenAICompatibilityProfile? profile = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), TestModels.Gpt4O, messages, tools.IsDefault ? [] : tools, toolChoice ?? LlmToolChoice.Auto, settings ?? LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), options ?? ProviderRequestOptions.Empty);
        return new OpenAIRequestTranslator().Translate(request, profile ?? NonStreamingProfile, useStreaming: false);
    }

    [Fact]
    public void Translate_WhenToolChoiceIsNone_SerializesNoneString()
    {
        var body = Translate([TestMessages.User("hi")], LlmToolChoice.None, [SampleTool]);
        body["tool_choice"]!.GetValue<string>().ShouldBe("none");
    }

    [Fact]
    public void Translate_WhenNoMessages_ProducesEmptyMessagesArray()
    {
        var body = Translate([]);
        body["messages"]!.AsArray().Count.ShouldBe(0);
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsReasoningPart_ThrowsNotSupportedException()
    {
        var reasoning = new ReasoningPart(new ReasoningContent("internal thoughts", ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning);
        _ = Should.Throw<NotSupportedException>(() => Translate([assistant]));
    }

    [Fact]
    public void Translate_WhenSystemMessageContainsUnknownPart_ThrowsNotSupportedException()
    {
        var unknown = new UnknownContentPart("vendor.special", JsonDocument.Parse("{}").RootElement, ExtensionData.Empty);
        var system = TestMessages.System("prefix");
        var withUnknown = system with
        {
            Parts = [unknown]
        };
        _ = Should.Throw<NotSupportedException>(() => Translate([withUnknown]));
    }

    [Fact]
    public void Translate_WhenDeveloperMessageAndSendAsSystemFlagIsTrue_UsesSystemRole()
    {
        var developer = new DeveloperMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001")), conversationId: null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), runId: null, turnId: null, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), MessageState.Complete, [new TextPart("Follow the house style.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var profileSendAsSystem = new OpenAICompatibilityProfile(NonStreamingProfile.BaseAddress, NonStreamingProfile.ChatCompletionsPath, sendDeveloperRoleAsSystem: true, NonStreamingProfile.PreferStreaming, NonStreamingProfile.IncludeStreamUsage, NonStreamingProfile.UseMaxCompletionTokensField, NonStreamingProfile.DefaultRequestHeaders);
        var body = Translate([developer], profile: profileSendAsSystem);
        body["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("system");
        var nativeBody = Translate([developer], profile: NonStreamingProfile);
        nativeBody["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("developer");
    }

    [Fact]
    public void Translate_WhenRuntimeMessagePresent_MapsToSystemRole()
    {
        var runtime = new RuntimeMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001")), conversationId: null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new TurnId(Guid.Parse("50000000-0000-0000-0000-000000000001")), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), MessageState.Complete, [new TextPart("The run was interrupted.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var body = Translate([runtime]);
        body["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("system");
    }

    [Fact]
    public void Translate_WhenToolMessageContainsMultipleResults_EmitsOneWireMessagePerResult()
    {
        var toolReference = new ToolReference(new ToolId("t"), null, "t");
        var callIdA = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
        var callIdB = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
        var toolMessage = TestMessages.Tool(new ToolResultPart(callIdA, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty), [new TextPart("result A", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty), new ToolResultPart(callIdB, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty), [new TextPart("result B", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty));
        var body = Translate([toolMessage]);
        var messages = body["messages"]!.AsArray();
        messages.Count.ShouldBe(2);
        messages[0]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdA.ToString());
        messages[0]!["content"]!.GetValue<string>().ShouldBe("result A");
        messages[1]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdB.ToString());
        messages[1]!["content"]!.GetValue<string>().ShouldBe("result B");
    }

    [Fact]
    public void Translate_WhenSettingsExtensionsContainUnknownKey_AddsItToRequestBody()
    {
        var extensionValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-123")]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("user", extensionValue));
        var settings = LlmRequestSettings.Default with
        {
            Extensions = extensions
        };
        var body = Translate([TestMessages.User("hi")], settings: settings);
        body["user"]!.GetValue<string>().ShouldBe("end-user-123");
    }

    [Fact]
    public void Translate_WhenProviderOptionsExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedModel = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("hacked-model")]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("model", hackedModel));
        var options = new ProviderRequestOptions(extensions);
        var body = Translate([TestMessages.User("hi")], options: options);
        body["model"]!.GetValue<string>().ShouldBe(TestModels.Gpt4O.ModelId.Value);
    }

    [Fact]
    public void Translate_WhenParallelToolCallsSettingIsFalse_SerializesFalse()
    {
        var settings = LlmRequestSettings.Default with
        {
            ParallelToolCalls = false
        };
        var body = Translate([TestMessages.User("hi")], tools: [SampleTool], settings: settings);
        body["parallel_tool_calls"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenUseMaxCompletionTokensFieldIsFalse_UsesLegacyMaxTokensField()
    {
        var settings = LlmRequestSettings.Default with
        {
            MaxOutputTokens = 256
        };
        var legacyProfile = new OpenAICompatibilityProfile(NonStreamingProfile.BaseAddress, NonStreamingProfile.ChatCompletionsPath, NonStreamingProfile.SendDeveloperRoleAsSystem, NonStreamingProfile.PreferStreaming, NonStreamingProfile.IncludeStreamUsage, useMaxCompletionTokensField: false, NonStreamingProfile.DefaultRequestHeaders);
        var body = Translate([TestMessages.User("hi")], settings: settings, profile: legacyProfile);
        body["max_tokens"]!.GetValue<long>().ShouldBe(256);
        body.ContainsKey("max_completion_tokens").ShouldBeFalse();
    }
}
