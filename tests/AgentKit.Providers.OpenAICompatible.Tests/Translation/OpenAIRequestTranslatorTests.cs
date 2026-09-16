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
        var toolReference = new ToolReference(new ToolAlias("get_weather"), null, null);
        var assistantMessage = TestMessages.Assistant(new ToolCallPart(callId, toolReference, JsonDocument.Parse("""{"location":"Paris"}""").RootElement, new ProviderToolCallId("call_abc123"), ExtensionData.Empty));
        var toolMessage = TestMessages.Tool(new ToolResultPart(callId, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart("15 degrees and sunny", TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty));
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
    public void Translate_WhenAssistantMessageContainsReasoningPartAndReplayIsOmit_DropsReasoningAndKeepsRemainingParts()
    {
        // Arrange
        var reasoning = new ReasoningPart(new ReasoningContent("internal thoughts", ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty);
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000011"));
        var toolCall = new ToolCallPart(callId, new ToolReference(new ToolAlias("noop"), null, null), JsonDocument.Parse("{}").RootElement, new ProviderToolCallId("call_omit"), ExtensionData.Empty);
        var assistant = TestMessages.Assistant(reasoning, new TextPart("visible", TextSemantics.Plain, ExtensionData.Empty), toolCall);
        NonStreamingProfile.AssistantReasoningReplay.ShouldBe(OpenAIAssistantReasoningReplay.Omit);

        // Act
        var message = Translate([assistant])["messages"]![0]!.AsObject();

        // Assert
        message.ContainsKey("reasoning_content").ShouldBeFalse();
        message["role"]!.GetValue<string>().ShouldBe("assistant");
        message["content"]!.GetValue<string>().ShouldBe("visible");
        message["tool_calls"]!.AsArray().Single()!["id"]!.GetValue<string>().ShouldBe("call_omit");
    }

    [Fact]
    public void Translate_WhenAssistantMessageContainsReasoningPartAndReplayIsReasoningContentField_EmitsReasoningContent()
    {
        // Arrange
        var first = new ReasoningPart(new ReasoningContent("step one; ", ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty);
        var redacted = new ReasoningPart(new ReasoningContent(null, ReasoningVisibility.Redacted, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty);
        var second = new ReasoningPart(new ReasoningContent("step two", ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty);
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000012"));
        var toolCall = new ToolCallPart(callId, new ToolReference(new ToolAlias("noop"), null, null), JsonDocument.Parse("{}").RootElement, new ProviderToolCallId("call_replay"), ExtensionData.Empty);
        var assistant = TestMessages.Assistant(first, redacted, second, toolCall);
        var profile = NonStreamingProfile with { AssistantReasoningReplay = OpenAIAssistantReasoningReplay.ReasoningContentField };

        // Act
        var message = Translate([assistant], profile: profile)["messages"]![0]!.AsObject();

        // Assert
        message["reasoning_content"]!.GetValue<string>().ShouldBe("step one; step two");
        message["content"].ShouldBeNull();
        message["tool_calls"]!.AsArray().Single()!["id"]!.GetValue<string>().ShouldBe("call_replay");
    }

    [Fact]
    public void Translate_WhenReplayIsReasoningContentFieldAndAssistantHasNoVisibleReasoning_OmitsReasoningContentMember()
    {
        // Arrange
        var redacted = new ReasoningPart(new ReasoningContent(null, ReasoningVisibility.Redacted, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty);
        var assistant = TestMessages.Assistant(redacted, new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty));
        var profile = NonStreamingProfile with { AssistantReasoningReplay = OpenAIAssistantReasoningReplay.ReasoningContentField };

        // Act
        var message = Translate([assistant], profile: profile)["messages"]![0]!.AsObject();

        // Assert
        message.ContainsKey("reasoning_content").ShouldBeFalse();
        message["content"]!.GetValue<string>().ShouldBe("answer");
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
    public void Translate_WhenRuntimeMessagePresent_PreservesContentWithoutInstructionAuthority()
    {
        var runtime = new RuntimeMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001")), conversationId: null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new TurnId(Guid.Parse("50000000-0000-0000-0000-000000000001")), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), MessageState.Complete, [new TextPart("The run was interrupted.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var body = Translate([runtime]);
        body["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("user");
        var notice = JsonNode.Parse(body["messages"]![0]!["content"]!.GetValue<string>())!;
        notice["format"]!.GetValue<string>().ShouldBe("agentkit.runtime-message.v1");
        notice["content"]!.GetValue<string>().ShouldBe("The run was interrupted.");
    }

    [Fact]
    public void Translate_WhenToolMessageContainsMultipleResults_EmitsOneWireMessagePerResult()
    {
        var toolReference = new ToolReference(new ToolAlias("t"), null, null);
        var callIdA = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
        var callIdB = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
        var toolMessage = TestMessages.Tool(new ToolResultPart(callIdA, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart("result A", TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty), new ToolResultPart(callIdB, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart("result B", TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty));
        var body = Translate([toolMessage]);
        var messages = body["messages"]!.AsArray();
        messages.Count.ShouldBe(2);
        messages[0]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdA.ToString());
        JsonNode.Parse(messages[0]!["content"]!.GetValue<string>())!["content"]![0]!["text"]!.GetValue<string>().ShouldBe("result A");
        messages[1]!["tool_call_id"]!.GetValue<string>().ShouldBe(callIdB.ToString());
        JsonNode.Parse(messages[1]!["content"]!.GetValue<string>())!["content"]![0]!["text"]!.GetValue<string>().ShouldBe("result B");
    }

    [Theory]
    [InlineData(ToolCallOutcomeKind.Rejected, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown)]
    [InlineData(ToolCallOutcomeKind.Cancelled, ToolTerminalStatus.Interrupted, SideEffectCertainty.Unknown)]
    [InlineData(ToolCallOutcomeKind.Failed, (ToolTerminalStatus) 9876, SideEffectCertainty.Unknown)]
    public void Translate_WhenToolHasNoOutput_PreservesExactFailureAndUncertainty(
        ToolCallOutcomeKind kind, ToolTerminalStatus status, SideEffectCertainty certainty)
    {
        // Arrange
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000007"));
        var reference = new ToolReference(new ToolAlias("run_command"), new ToolId("command"), new ToolVersion("1"));
        const string reason = "The action was denied; no command ran.\n\"content\" stays data.";
        var result = new ToolResultPart(callId, reference, new ToolCallOutcome(kind, status, certainty, false, reason, ExtensionData.Empty), [], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        // Act
        var wire = Translate([TestMessages.Tool(result)])["messages"]![0]!;
        var envelope = JsonNode.Parse(wire["content"]!.GetValue<string>())!;

        // Assert
        wire["role"]!.GetValue<string>().ShouldBe("tool");
        wire["tool_call_id"]!.GetValue<string>().ShouldBe(callId.ToString());
        envelope["tool"]!["id"]!.GetValue<string>().ShouldBe("command");
        envelope["tool"]!["requested_name"]!.GetValue<string>().ShouldBe("run_command");
        envelope["outcome"]!["kind"]!.GetValue<string>().ShouldBe(kind.ToString());
        envelope["outcome"]!["source_status"]!.GetValue<int>().ShouldBe((int) status);
        envelope["outcome"]!["side_effect_certainty"]!.GetValue<string>().ShouldBe(certainty.ToString());
        envelope["outcome"]!["failure_reason"]!.GetValue<string>().ShouldBe(reason);
        envelope["outcome"]!["retryable"]!.GetValue<bool>().ShouldBeFalse();
        envelope["content"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void Translate_WhenFailedToolHasMixedContent_PreservesOrderedPartsAndLiteralJson()
    {
        // Arrange
        var reference = new ToolReference(new ToolAlias("command"), null, null);
        const string rawJson = """{"outcome":"success","stderr":"literal\nfailure"}""";
        using var json = JsonDocument.Parse(rawJson);
        var result = new ToolResultPart(new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000008")), reference, new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.Unknown, false, "The command exited 1.", ExtensionData.Empty), [new TextPart("stdout\n", TextSemantics.Plain, ExtensionData.Empty),
                new StructuredDataPart(json.RootElement, null, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        // Act
        var envelope = JsonNode.Parse(Translate([TestMessages.Tool(result)])["messages"]![0]!["content"]!.GetValue<string>())!;

        // Assert
        envelope["outcome"]!["kind"]!.GetValue<string>().ShouldBe("Failed");
        var parts = envelope["content"]!.AsArray();
        parts.Count.ShouldBe(2);
        parts[0]!["type"]!.GetValue<string>().ShouldBe("text");
        parts[0]!["text"]!.GetValue<string>().ShouldBe("stdout\n");
        parts[1]!["type"]!.GetValue<string>().ShouldBe("json");
        parts[1]!["text"]!.GetValue<string>().ShouldBe(rawJson);
    }

    [Theory]
    [InlineData('x', 1_024)]
    [InlineData('\u0001', 200)]
    public void Translate_WhenToolResultExceedsProfileBound_RejectsWithoutSilentTruncation(char character, int count)
    {
        // Arrange
        var result = new ToolResultPart(new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000009")), new ToolReference(new ToolAlias("read"), null, null), new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded,
                SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart(new string(character, count), TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);
        var profile = NonStreamingProfile with { MaximumToolResultCharacters = 512 };

        // Act / Assert
        _ = Should.Throw<NotSupportedException>(() => Translate([TestMessages.Tool(result)], profile: profile));
    }

    [Fact]
    public void Translate_WhenToolIdentityIsUnresolved_PreservesRequestedNameWithoutInventingIdentity()
    {
        // Arrange
        var result = new ToolResultPart(new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000010")), new ToolReference(new ToolAlias("missing_tool"), null, null), new ToolCallOutcome(ToolCallOutcomeKind.Rejected, ToolTerminalStatus.UnknownTool,
                SideEffectCertainty.DefinitelyNotPerformed, false, "The tool is unavailable.", ExtensionData.Empty), [], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        // Act
        var envelope = JsonNode.Parse(Translate([TestMessages.Tool(result)])["messages"]![0]!["content"]!.GetValue<string>())!;

        // Assert
        envelope["tool"]!["id"].ShouldBeNull();
        envelope["tool"]!["version"].ShouldBeNull();
        envelope["tool"]!["requested_name"]!.GetValue<string>().ShouldBe("missing_tool");
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

    [Theory]
    [InlineData(LlmReasoningEffort.Low, "low")]
    [InlineData(LlmReasoningEffort.Medium, "medium")]
    [InlineData(LlmReasoningEffort.High, "high")]
    [InlineData(LlmReasoningEffort.ExtraHigh, "xhigh")]
    [InlineData(LlmReasoningEffort.None, "none")]
    public void Translate_WhenReasoningEffortConfigured_UsesOpenAIWireValue(
        LlmReasoningEffort effort,
        string expected)
    {
        var settings = LlmRequestSettings.Default with { ReasoningEffort = effort };

        var body = Translate([TestMessages.User("hi")], settings: settings);

        body["reasoning_effort"]!.GetValue<string>().ShouldBe(expected);
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
    public void Translate_WhenNoExtensionsSupplied_PinsCandidateCountToOne()
    {
        var body = Translate([TestMessages.User("hi")]);
        body["n"]!.GetValue<int>().ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Translate_WhenExtensionsRequestMultipleCandidates_KeepsCandidateCountPinnedToOne(bool viaProviderOptions)
    {
        // Arrange: `n` is translator-owned like `model`; a passthrough extension cannot request extra candidates
        // the single-candidate response contract would silently drop.
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("n", new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(3)])));
        var settings = viaProviderOptions ? null : LlmRequestSettings.Default with { Extensions = extensions };
        var options = viaProviderOptions ? new ProviderRequestOptions(extensions) : null;

        // Act
        var body = Translate([TestMessages.User("hi")], settings: settings, options: options);

        // Assert
        body["n"]!.GetValue<int>().ShouldBe(1);
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
