// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Translation;

/// <summary>Verifies native schema translation on <see cref="OpenAIRequestTranslator"/>.</summary>
public sealed class OpenAIRequestTranslatorStructuredOutputTests
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
    public void Translate_WhenNativeSchemaOutputIsPresent_EmitsJsonSchemaResponseFormat()
    {
        var schema = JsonDocument.Parse("""{"type":"object","properties":{"answer":{"type":"string"}}}""").RootElement;
        var output = new OutputDefinition(
            new OutputDefinitionId("answer"),
            new OutputDefinitionVersion("1"),
            "answer",
            OutputMode.NativeSchema,
            new JsonSchemaDocument("answer", new SchemaVersion("1"), schema),
            runtimeType: null,
            [],
            [],
            OutputValidationPolicy.RejectOnFirstFailure,
            OutputRetryPolicy.None,
            OutputEndStrategy.Graceful);
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
            [TestMessages.User("Return JSON.")],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty)
        {
            Output = output,
        };
        var request = new LlmModelRequest(context, attempt: 1, DateTimeOffset.UtcNow.AddMinutes(1), ProviderRequestOptions.Empty);
        var body = new OpenAIRequestTranslator().Translate(request, Profile, useStreaming: false);
        body["response_format"]!["type"]!.GetValue<string>().ShouldBe("json_schema");
        body["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>().ShouldBeTrue();
    }
}
