// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

/// <summary>
/// The default <see cref="IMistralAIRequestTranslator"/>, covering the
/// content roles, parts, tool declarations, tool configuration, and
/// generation settings supported by the Mistral AI Chat Completions wire
/// format.
/// </summary>
/// <remarks>
/// Mistral accepts only nine-character alphanumeric tool-call identifiers.
/// The translator therefore allocates one wire identifier per canonical
/// <see cref="ToolCallId"/> for the whole request (see
/// <see cref="MistralAIToolCallIdCodec"/>), keeping a preserved
/// <see cref="ToolCallPart.ProviderCallId"/> only when it already has that
/// shape, and uses the same identifier for the assistant
/// <c>tool_calls[].id</c> and the tool <c>tool_call_id</c>.
/// </remarks>
public sealed class MistralAIRequestTranslator: IMistralAIRequestTranslator
{
    /// <summary>
    /// The number of successive disambiguators tried before a derived wire
    /// identifier collision is reported as untranslatable.
    /// </summary>
    private const int _maxDisambiguationAttempts = 16;

    /// <inheritdoc/>
    public JsonObject Translate(LlmModelRequest request, bool useStreaming)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var wireCallIds = AllocateWireCallIds(context.Messages);

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["messages"] = TranslateMessages(context.Messages, wireCallIds),
            ["stream"] = useStreaming,
        };

        if (context.Tools.Length > 0)
        {
            body["tools"] = TranslateTools(context.Tools);
            body["tool_choice"] = TranslateToolChoice(context.ToolChoice);
        }

        if (context.Settings.ParallelToolCalls is { } parallelToolCalls)
        {
            body["parallel_tool_calls"] = parallelToolCalls;
        }

        ApplySettings(body, context.Settings);
        ApplyExtensions(body, context.Settings.Extensions);
        ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static void ApplySettings(JsonObject body, LlmRequestSettings settings)
    {
        if (settings.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        if (settings.TopP is { } topP)
        {
            body["top_p"] = topP;
        }

        if (settings.MaxOutputTokens is { } maxOutputTokens)
        {
            body["max_tokens"] = maxOutputTokens;
        }

        if (settings.StopSequences.Length > 0)
        {
            var stop = new JsonArray();
            foreach (var stopSequence in settings.StopSequences)
            {
                stop.Add(JsonValue.Create(stopSequence));
            }

            body["stop"] = stop;
        }

        if (settings.Seed is { } seed)
        {
            body["random_seed"] = seed;
        }
    }

    private static void ApplyExtensions(JsonObject body, ExtensionData extensions)
    {
        foreach (var (key, value) in extensions.Values)
        {
            // Extension data never overrides a field the translator itself
            // owns; a protected core field cannot be reshaped by a
            // passthrough option.
            if (body.ContainsKey(key))
            {
                continue;
            }

            body[key] = JsonNode.Parse(value.CanonicalJson.AsSpan());
        }
    }

    /// <summary>
    /// Allocates, once per request, the nine-character wire identifier used
    /// for every tool call and tool result in <paramref name="messages"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first pass keeps a preserved <see cref="ToolCallPart.ProviderCallId"/>
    /// only when it already satisfies Mistral's
    /// <c>^[a-zA-Z0-9]{9}$</c> constraint and no earlier call in the request
    /// claimed the same value; an identifier minted by another provider (or a
    /// duplicate) is not a Mistral identity and is discarded. The second pass
    /// derives an identifier through <see cref="MistralAIToolCallIdCodec"/> for
    /// every remaining call and result in encounter order, incrementing the
    /// disambiguator while the derived value is already claimed, so distinct
    /// canonical identities never share a wire identifier within one request.
    /// </para>
    /// <para>
    /// Exhausting <see cref="_maxDisambiguationAttempts"/> consecutive
    /// collisions is treated as an untranslatable request rather than
    /// silently emitting a duplicate identifier.
    /// </para>
    /// </remarks>
    /// <param name="messages">The complete request history.</param>
    /// <returns>A map from every canonical identity in the history to its unique wire identifier.</returns>
    /// <exception cref="NotSupportedException">
    /// No distinct wire identifier could be derived for a call within
    /// <see cref="_maxDisambiguationAttempts"/> attempts.
    /// </exception>
    private static Dictionary<ToolCallId, string> AllocateWireCallIds(ImmutableArray<AgentMessage> messages)
    {
        var map = new Dictionary<ToolCallId, string>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                if (part is ToolCallPart toolCall
                    && toolCall.ProviderCallId?.Value is { } providerCallId
                    && MistralAIToolCallIdCodec.IsWireId(providerCallId)
                    && !map.ContainsKey(toolCall.CallId)
                    && claimed.Add(providerCallId))
                {
                    map[toolCall.CallId] = providerCallId;
                }
            }
        }

        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                var callId = part switch
                {
                    ToolCallPart toolCall => toolCall.CallId,
                    ToolResultPart toolResult => toolResult.CallId,
                    _ => default(ToolCallId?),
                };

                if (callId is { } unmapped && !map.ContainsKey(unmapped))
                {
                    map[unmapped] = AllocateDerivedWireId(unmapped, claimed);
                }
            }
        }

        return map;
    }

    private static string AllocateDerivedWireId(ToolCallId callId, HashSet<string> claimed)
    {
        Debug.Assert(callId != default, "Only real identities are allocated wire identifiers.");

        for (var disambiguator = 0; disambiguator < _maxDisambiguationAttempts; disambiguator++)
        {
            var candidate = MistralAIToolCallIdCodec.Encode(callId, disambiguator);
            if (claimed.Add(candidate))
            {
                return candidate;
            }
        }

        throw new NotSupportedException(
            $"A distinct nine-character Mistral AI tool-call identifier could not be derived for tool call " +
            $"'{callId}' after {_maxDisambiguationAttempts} attempts because every candidate collides with " +
            "another identifier in the same request.");
    }

    private static JsonArray TranslateMessages(
        ImmutableArray<AgentMessage> messages,
        Dictionary<ToolCallId, string> providerCallIds)
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
                    result.Add(new JsonObject { ["role"] = "system", ["content"] = JoinText(message.Parts, message) });
                    break;

                case UserMessage:
                case RuntimeMessage:
                    result.Add(new JsonObject { ["role"] = "user", ["content"] = JoinText(message.Parts, message) });
                    break;

                case AssistantMessage:
                    result.Add(TranslateAssistantMessage(message.Parts, providerCallIds));
                    break;

                case ToolMessage:
                    foreach (var toolResultMessage in TranslateToolMessage(message.Parts, providerCallIds))
                    {
                        result.Add(toolResultMessage);
                    }

                    break;

                default:
                    throw new NotSupportedException(
                        $"Message kind '{message.GetType().Name}' is not supported by the Mistral AI Chat " +
                        "Completions request translator.");
            }
        }

        return result;
    }

    private static string JoinText(ImmutableArray<ContentPart> parts, AgentMessage owner)
    {
        var text = new StringBuilder();

        foreach (var part in parts)
        {
            if (part is not TextPart textPart)
            {
                throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a {owner.GetType().Name} " +
                    "by the Mistral AI Chat Completions request translator.");
            }

            if (text.Length > 0)
            {
                _ = text.Append('\n').Append('\n');
            }

            _ = text.Append(textPart.Text);
        }

        return text.ToString();
    }

    private static JsonObject TranslateAssistantMessage(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        var contentChunks = new JsonArray();
        var toolCalls = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    contentChunks.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                    break;

                case ToolCallPart toolCall:
                    toolCalls.Add(new JsonObject
                    {
                        ["id"] = providerCallIds[toolCall.CallId],
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = toolCall.Tool.Name,
                            ["arguments"] = toolCall.Arguments.GetRawText(),
                        },
                    });
                    break;

                case ReasoningPart:
                    throw new NotSupportedException(
                        "Reasoning content is not yet supported by the Mistral AI Chat Completions request " +
                        "translator.");

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in an assistant message " +
                        "by the Mistral AI Chat Completions request translator.");
            }
        }

        var message = new JsonObject { ["role"] = "assistant" };
        message["content"] = contentChunks.Count > 0 ? contentChunks : null;

        if (toolCalls.Count > 0)
        {
            message["tool_calls"] = toolCalls;
        }

        return message;
    }

    private static IEnumerable<JsonObject> TranslateToolMessage(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        foreach (var part in parts)
        {
            yield return part is not ToolResultPart toolResult
                ? throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a tool message by the " +
                    "Mistral AI Chat Completions request translator.")
                : new JsonObject
                {
                    ["role"] = "tool",
                    ["content"] = BuildToolResultContent(toolResult),
                    ["tool_call_id"] = providerCallIds[toolResult.CallId],
                    ["name"] = toolResult.Tool.Name,
                };
        }
    }

    private static string BuildToolResultContent(ToolResultPart toolResult)
    {
        if (toolResult.Outcome.Kind != ToolCallOutcomeKind.Success)
        {
            return $"Error: {toolResult.Outcome.FailureReason ?? "The tool call failed."}";
        }

        foreach (var contentPart in toolResult.Content)
        {
            if (contentPart is StructuredDataPart structuredData)
            {
                return structuredData.Value.GetRawText();
            }
        }

        var text = new StringBuilder();
        foreach (var contentPart in toolResult.Content)
        {
            if (contentPart is not TextPart textPart)
            {
                throw new NotSupportedException(
                    $"Tool result content part kind '{contentPart.GetType().Name}' is not supported by the " +
                    "Mistral AI Chat Completions request translator.");
            }

            _ = text.Append(textPart.Text);
        }

        return text.ToString();
    }

    private static JsonArray TranslateTools(ImmutableArray<LlmToolDefinition> tools)
    {
        var result = new JsonArray();

        foreach (var tool in tools)
        {
            var function = new JsonObject
            {
                ["name"] = tool.Name,
                ["parameters"] = JsonNode.Parse(tool.ParametersSchema.GetRawText()),
            };

            if (tool.Description is { } description)
            {
                function["description"] = description;
            }

            result.Add(new JsonObject { ["type"] = "function", ["function"] = function });
        }

        return result;
    }

    private static JsonNode TranslateToolChoice(LlmToolChoice toolChoice) =>
        toolChoice.Mode switch
        {
            LlmToolChoiceMode.Auto => JsonValue.Create("auto"),
            LlmToolChoiceMode.None => JsonValue.Create("none"),
            LlmToolChoiceMode.Required => JsonValue.Create("any"),
            LlmToolChoiceMode.Named => new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = toolChoice.ForcedToolName },
            },
            _ => throw new NotSupportedException(
                $"Tool choice mode '{toolChoice.Mode}' is not supported by the Mistral AI Chat Completions " +
                "request translator."),
        };
}
