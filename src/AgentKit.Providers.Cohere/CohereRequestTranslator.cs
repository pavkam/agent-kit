// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// The default <see cref="ICohereRequestTranslator"/>, covering the
/// content roles, parts, tool declarations, tool configuration, and
/// generation settings supported by the Cohere v2 Chat wire format.
/// </summary>
public sealed class CohereRequestTranslator: ICohereRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(LlmModelRequest request, bool useStreaming)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var providerCallIds = CollectProviderCallIds(context.Messages);

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["messages"] = TranslateMessages(context.Messages, providerCallIds),
            ["stream"] = useStreaming,
        };

        if (context.Tools.Length > 0)
        {
            body["tools"] = TranslateTools(context.Tools);
        }

        var toolChoice = TranslateToolChoice(context.ToolChoice);
        if (toolChoice is not null)
        {
            body["tool_choice"] = toolChoice;
        }

        if (context.Settings.ParallelToolCalls is not null)
        {
            throw new NotSupportedException(
                "The Cohere v2 Chat API does not expose a parallel-tool-call control.");
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
            body["p"] = topP;
        }

        if (settings.MaxOutputTokens is { } maxOutputTokens)
        {
            body["max_tokens"] = maxOutputTokens;
        }

        if (settings.StopSequences.Length > 0)
        {
            var stopSequences = new JsonArray();
            foreach (var stopSequence in settings.StopSequences)
            {
                stopSequences.Add(JsonValue.Create(stopSequence));
            }

            body["stop_sequences"] = stopSequences;
        }

        if (settings.Seed is { } seed)
        {
            body["seed"] = seed;
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

    private static Dictionary<ToolCallId, string> CollectProviderCallIds(ImmutableArray<AgentMessage> messages)
    {
        var map = new Dictionary<ToolCallId, string>();

        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                if (part is ToolCallPart toolCall)
                {
                    map[toolCall.CallId] = toolCall.ProviderCallId?.Value ?? toolCall.CallId.ToString();
                }
            }
        }

        return map;
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
                        $"Message kind '{message.GetType().Name}' is not supported by the Cohere v2 Chat " +
                        "request translator.");
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
                    "by the Cohere v2 Chat request translator.");
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
        var contentBlocks = new JsonArray();
        var toolCalls = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    contentBlocks.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                    break;

                case ReasoningPart reasoning when reasoning.Content.Visibility == ReasoningVisibility.Visible:
                    contentBlocks.Add(new JsonObject { ["type"] = "thinking", ["thinking"] = reasoning.Content.Text ?? string.Empty });
                    break;

                case ReasoningPart reasoning:
                    throw new NotSupportedException(
                        $"Reasoning visibility '{reasoning.Content.Visibility}' has no Cohere content-block " +
                        "equivalent.");

                case ToolCallPart toolCall:
                    toolCalls.Add(new JsonObject
                    {
                        ["id"] = providerCallIds.GetValueOrDefault(toolCall.CallId, toolCall.CallId.ToString()),
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = toolCall.Tool.Name,
                            ["arguments"] = toolCall.Arguments.GetRawText(),
                        },
                    });
                    break;

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in an assistant message " +
                        "by the Cohere v2 Chat request translator.");
            }
        }

        var message = new JsonObject { ["role"] = "assistant" };
        if (contentBlocks.Count > 0)
        {
            message["content"] = contentBlocks;
        }

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
                    "Cohere v2 Chat request translator.")
                : new JsonObject
                {
                    ["role"] = "tool",
                    ["content"] = BuildToolResultContent(toolResult),
                    ["tool_call_id"] = providerCallIds.GetValueOrDefault(toolResult.CallId, toolResult.CallId.ToString()),
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
                    "Cohere v2 Chat request translator.");
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

    private static JsonValue? TranslateToolChoice(LlmToolChoice toolChoice) =>
        toolChoice.Mode switch
        {
            LlmToolChoiceMode.Auto => null,
            LlmToolChoiceMode.None => JsonValue.Create("NONE"),
            LlmToolChoiceMode.Required => JsonValue.Create("REQUIRED"),
            LlmToolChoiceMode.Named => throw new NotSupportedException(
                "The Cohere v2 Chat API cannot force a single specific tool by name; only REQUIRED (any tool) " +
                "and NONE are supported."),
            _ => throw new NotSupportedException(
                $"Tool choice mode '{toolChoice.Mode}' is not supported by the Cohere v2 Chat request " +
                "translator."),
        };
}
