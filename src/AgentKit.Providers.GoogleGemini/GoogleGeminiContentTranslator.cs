// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// The default <see cref="IGoogleGeminiContentTranslator"/>, covering the
/// content roles, parts, tool declarations, tool configuration, and
/// generation settings supported by the Gemini GenerateContent wire format.
/// </summary>
/// <remarks>
/// Gemini's <c>functionCallingConfig</c> exposes only a <c>mode</c>
/// (<c>AUTO</c>/<c>ANY</c>/<c>NONE</c>/<c>VALIDATED</c>) and
/// <c>allowedFunctionNames</c>; it has no field that toggles parallel
/// function calling, and the API already permits multiple
/// <c>functionCall</c> parts in a single response with no request-side
/// control. A request that leaves
/// <see cref="LlmRequestSettings.ParallelToolCalls"/> unset or
/// sets it to <see langword="true"/> is therefore translated with no
/// wire-format change: Gemini already behaves that way by default. A
/// request that sets it to <see langword="false"/> cannot be honored, since
/// there is no way to forbid parallel calls, so translation throws
/// <see cref="NotSupportedException"/> rather than silently ignoring the
/// caller's requirement.
/// </remarks>
public sealed class GoogleGeminiContentTranslator: IGoogleGeminiContentTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(LlmModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var providerCallIds = ProviderToolCallIds.Collect(context.Messages);
        var system = new StringBuilder();

        var body = new JsonObject
        {
            ["contents"] = TranslateMessages(context.Messages, providerCallIds, system),
        };

        if (system.Length > 0)
        {
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = system.ToString() }),
            };
        }

        if (context.Tools.Length > 0)
        {
            body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = TranslateTools(context.Tools) });
            body["toolConfig"] = TranslateToolChoice(context.ToolChoice);
        }

        if (context.Settings.ParallelToolCalls is false)
        {
            throw new NotSupportedException(
                "Settings.ParallelToolCalls=false is not supported by the Gemini GenerateContent API: " +
                "functionCallingConfig exposes no control to forbid parallel function calls.");
        }

        var generationConfig = TranslateGenerationConfig(context.Settings);
        if (generationConfig.Count > 0)
        {
            body["generationConfig"] = generationConfig;
        }

        ProviderJson.ApplyExtensions(body, context.Settings.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static JsonObject TranslateGenerationConfig(LlmRequestSettings settings)
    {
        var generationConfig = new JsonObject();

        if (settings.Temperature is { } temperature)
        {
            generationConfig["temperature"] = temperature;
        }

        if (settings.TopP is { } topP)
        {
            generationConfig["topP"] = topP;
        }

        if (settings.MaxOutputTokens is { } maxOutputTokens)
        {
            generationConfig["maxOutputTokens"] = maxOutputTokens;
        }

        if (settings.StopSequences.Length > 0)
        {
            var stop = new JsonArray();
            foreach (var stopSequence in settings.StopSequences)
            {
                stop.Add(JsonValue.Create(stopSequence));
            }

            generationConfig["stopSequences"] = stop;
        }

        if (settings.Seed is { } seed)
        {
            generationConfig["seed"] = seed;
        }

        return generationConfig;
    }

    private static JsonArray TranslateMessages(
        ImmutableArray<AgentMessage> messages,
        Dictionary<ToolCallId, string> providerCallIds,
        StringBuilder system)
    {
        var result = new JsonArray();

        foreach (var message in messages)
        {
            if (message.State != MessageState.Complete)
            {
                continue;
            }

            switch (message)
            {
                case SystemMessage:
                case DeveloperMessage:
                    AppendSystemText(system, message.Parts);
                    break;

                case UserMessage:
                    result.Add(CreateContent("user", TranslateUserParts(message.Parts)));
                    break;

                case RuntimeMessage:
                    result.Add(CreateContent("user", TranslateRuntimeParts(message.Parts)));
                    break;

                case AssistantMessage:
                    result.Add(CreateContent("model", TranslateAssistantParts(message.Parts, providerCallIds)));
                    break;

                case ToolMessage:
                    result.Add(CreateContent("user", TranslateToolResultParts(message.Parts, providerCallIds)));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Message kind '{message.GetType().Name}' is not supported by the Gemini " +
                        "GenerateContent request translator.");
            }
        }

        return result;
    }

    private static void AppendSystemText(StringBuilder system, ImmutableArray<ContentPart> parts)
    {
        foreach (var part in parts)
        {
            if (part is not TextPart text)
            {
                throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a system or developer " +
                    "message by the Gemini GenerateContent request translator.");
            }

            if (system.Length > 0)
            {
                _ = system.Append("\n\n");
            }

            _ = system.Append(text.Text);
        }
    }

    private static JsonObject CreateContent(string role, JsonArray parts) =>
        new()
        {
            ["role"] = role,
            ["parts"] = parts,
        };

    private static JsonArray TranslateUserParts(ImmutableArray<ContentPart> parts)
    {
        var result = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    result.Add(new JsonObject { ["text"] = text.Text });
                    break;

                case MediaReferencePart:
                    throw new NotSupportedException(
                        "Image and file input is not yet supported by the Gemini GenerateContent request " +
                        "translator.");

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in a user message " +
                        "by the Gemini GenerateContent request translator.");
            }
        }

        return result;
    }

    private static JsonArray TranslateRuntimeParts(ImmutableArray<ContentPart> parts) =>
        [new JsonObject { ["text"] = RuntimeMessageProjection.BuildEnvelopeJson(parts) }];

    private static JsonArray TranslateAssistantParts(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        var result = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    result.Add(WithThoughtSignature(new JsonObject { ["text"] = text.Text }, text.Extensions));
                    break;

                case ToolCallPart toolCall:
                    result.Add(WithThoughtSignature(
                        new JsonObject
                        {
                            ["functionCall"] = new JsonObject
                            {
                                ["id"] = providerCallIds.GetValueOrDefault(toolCall.CallId, toolCall.CallId.ToString()),
                                ["name"] = toolCall.Tool.Name,
                                ["args"] = JsonNode.Parse(toolCall.Arguments.GetRawText()),
                            },
                        },
                        toolCall.Extensions));
                    break;

                case ReasoningPart reasoning when reasoning.Content.Visibility == ReasoningVisibility.Visible:
                    result.Add(new JsonObject
                    {
                        ["text"] = reasoning.Content.Text ?? string.Empty,
                        ["thought"] = true,
                        ["thoughtSignature"] = reasoning.Content.SignatureToken,
                    });
                    break;

                case ReasoningPart reasoning:
                    throw new NotSupportedException(
                        $"Reasoning visibility '{reasoning.Content.Visibility}' has no Gemini part " +
                        "equivalent.");

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in a model message " +
                        "by the Gemini GenerateContent request translator.");
            }
        }

        return result;
    }

    /// <summary>
    /// Re-emits the <c>thoughtSignature</c> a model part carried, if any, as a sibling field on the same wire
    /// part. Gemini requires the signature to be returned inside its original part, so it is never moved to a
    /// separate part or merged with another part's signature.
    /// </summary>
    /// <param name="wirePart">The <c>text</c> or <c>functionCall</c> wire part being emitted.</param>
    /// <param name="extensions">The source part's extension data.</param>
    /// <returns><paramref name="wirePart"/>, with <c>thoughtSignature</c> added when one was retained.</returns>
    private static JsonObject WithThoughtSignature(JsonObject wirePart, ExtensionData extensions)
    {
        Debug.Assert(wirePart is not null, "Callers supply the wire part they are about to emit.");
        Debug.Assert(extensions is not null, "Content parts always carry non-null extension data.");

        if (GoogleGeminiThoughtSignature.TryRead(extensions) is { } signature)
        {
            wirePart["thoughtSignature"] = signature;
        }

        return wirePart;
    }

    private static JsonArray TranslateToolResultParts(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        var result = new JsonArray();

        foreach (var part in parts)
        {
            if (part is not ToolResultPart toolResult)
            {
                throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a tool message by " +
                    "the Gemini GenerateContent request translator.");
            }

            result.Add(new JsonObject
            {
                ["functionResponse"] = new JsonObject
                {
                    ["id"] = providerCallIds.GetValueOrDefault(toolResult.CallId, toolResult.CallId.ToString()),
                    ["name"] = toolResult.Tool.Name,
                    ["response"] = BuildFunctionResponsePayload(toolResult),
                },
            });
        }

        return result;
    }

    private static JsonObject BuildFunctionResponsePayload(ToolResultPart toolResult)
    {
        if (toolResult.Outcome.Kind != ToolCallOutcomeKind.Success)
        {
            return new JsonObject { ["error"] = toolResult.Outcome.FailureReason ?? "The tool call failed." };
        }

        foreach (var contentPart in toolResult.Content)
        {
            if (contentPart is StructuredDataPart structuredData)
            {
                return new JsonObject { ["result"] = JsonNode.Parse(structuredData.Value.GetRawText()) };
            }
        }

        var text = new StringBuilder();
        foreach (var contentPart in toolResult.Content)
        {
            if (contentPart is not TextPart textPart)
            {
                throw new NotSupportedException(
                    $"Tool result content part kind '{contentPart.GetType().Name}' is not supported by " +
                    "the Gemini GenerateContent request translator.");
            }

            _ = text.Append(textPart.Text);
        }

        return new JsonObject { ["result"] = text.ToString() };
    }

    private static JsonArray TranslateTools(ImmutableArray<LlmToolDefinition> tools)
    {
        var result = new JsonArray();

        foreach (var tool in tools)
        {
            var declaration = new JsonObject
            {
                ["name"] = tool.Name,
                ["parameters"] = JsonNode.Parse(tool.ParametersSchema.GetRawText()),
            };

            if (tool.Description is { } description)
            {
                declaration["description"] = description;
            }

            result.Add(declaration);
        }

        return result;
    }

    private static JsonObject TranslateToolChoice(LlmToolChoice toolChoice)
    {
        var functionCallingConfig = toolChoice.Mode switch
        {
            LlmToolChoiceMode.Auto => new JsonObject { ["mode"] = "AUTO" },
            LlmToolChoiceMode.None => new JsonObject { ["mode"] = "NONE" },
            LlmToolChoiceMode.Required => new JsonObject { ["mode"] = "ANY" },
            LlmToolChoiceMode.Named => new JsonObject
            {
                ["mode"] = "ANY",
                ["allowedFunctionNames"] = new JsonArray(JsonValue.Create(toolChoice.ForcedToolName)),
            },
            _ => throw new NotSupportedException(
                $"Tool choice mode '{toolChoice.Mode}' is not supported by the Gemini GenerateContent " +
                "request translator."),
        };

        return new JsonObject { ["functionCallingConfig"] = functionCallingConfig };
    }
}
