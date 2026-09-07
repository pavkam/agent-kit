// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The default <see cref="IOpenAIRequestTranslator"/>, covering the message
/// roles, tool definitions, tool choice, and sampling settings supported by
/// the OpenAI-compatible Chat Completions wire format.
/// </summary>
public sealed class OpenAIRequestTranslator: IOpenAIRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(LlmModelRequest request, OpenAICompatibilityProfile profile, bool useStreaming)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);

        var context = request.Context;
        var providerCallIds = CollectProviderCallIds(context.Messages);

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["messages"] = TranslateMessages(context.Messages, profile, providerCallIds),
        };

        if (useStreaming)
        {
            body["stream"] = true;

            if (profile.IncludeStreamUsage)
            {
                body["stream_options"] = new JsonObject { ["include_usage"] = true };
            }
        }
        else
        {
            body["stream"] = false;
        }

        if (context.Tools.Length > 0)
        {
            body["tools"] = TranslateTools(context.Tools);
            body["tool_choice"] = TranslateToolChoice(context.ToolChoice);

            if (context.Settings.ParallelToolCalls is { } parallelToolCalls)
            {
                body["parallel_tool_calls"] = parallelToolCalls;
            }
        }

        if (context.Settings.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        if (context.Settings.TopP is { } topP)
        {
            body["top_p"] = topP;
        }

        if (context.Settings.MaxOutputTokens is { } maxOutputTokens)
        {
            body[profile.UseMaxCompletionTokensField ? "max_completion_tokens" : "max_tokens"] = maxOutputTokens;
        }

        if (context.Settings.StopSequences.Length > 0)
        {
            var stop = new JsonArray();
            foreach (var stopSequence in context.Settings.StopSequences)
            {
                stop.Add(JsonValue.Create(stopSequence));
            }

            body["stop"] = stop;
        }

        if (context.Settings.Seed is { } seed)
        {
            body["seed"] = seed;
        }

        ApplyExtensions(body, context.Settings.Extensions);
        ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static void ApplyExtensions(JsonObject body, ExtensionData extensions)
    {
        foreach (var (key, value) in extensions.Values)
        {
            // Extension data never overrides a field the translator itself
            // owns; a protected core field cannot be reshaped by a passthrough
            // option.
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
        OpenAICompatibilityProfile profile,
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
                    result.Add(CreateTextMessage("system", JoinText(message.Parts)));
                    break;

                case DeveloperMessage:
                    result.Add(CreateTextMessage(
                        profile.SendDeveloperRoleAsSystem ? "system" : "developer",
                        JoinText(message.Parts)));
                    break;

                case UserMessage:
                    result.Add(CreateTextMessage("user", JoinUserContent(message.Parts)));
                    break;

                case RuntimeMessage:
                    result.Add(CreateTextMessage("system", JoinText(message.Parts)));
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
                        $"Message kind '{message.GetType().Name}' is not supported by the " +
                        "OpenAI-compatible request translator.");
            }
        }

        return result;
    }

    private static JsonObject CreateTextMessage(string role, string content) =>
        new()
        {
            ["role"] = role,
            ["content"] = content,
        };

    private static string JoinText(ImmutableArray<ContentPart> parts)
    {
        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            _ = part switch
            {
                TextPart text => builder.Append(text.Text),
                _ => throw new NotSupportedException(
                                        $"Content part kind '{part.GetType().Name}' is not supported here by the " +
                                        "OpenAI-compatible request translator."),
            };
        }

        return builder.ToString();
    }

    private static string JoinUserContent(ImmutableArray<ContentPart> parts)
    {
        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            _ = part switch
            {
                TextPart text => builder.Append(text.Text),
                MediaReferencePart => throw new NotSupportedException(
                                        "Image and other media input is not yet supported by the OpenAI-compatible " +
                                        "request translator."),
                _ => throw new NotSupportedException(
                                        $"Content part kind '{part.GetType().Name}' is not supported here by the " +
                                        "OpenAI-compatible request translator."),
            };
        }

        return builder.ToString();
    }

    private static JsonObject TranslateAssistantMessage(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        var text = new StringBuilder();
        JsonArray? toolCalls = null;

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart textPart:
                    _ = text.Append(textPart.Text);
                    break;

                case ToolCallPart toolCall:
                    toolCalls ??= [];
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
                        $"Content part kind '{part.GetType().Name}' is not supported in an assistant " +
                        "message by the OpenAI-compatible request translator.");
            }
        }

        var message = new JsonObject { ["role"] = "assistant" };

        message["content"] = text.Length > 0 || toolCalls is null
            ? text.ToString()
            : null;

        if (toolCalls is not null)
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
            if (part is not ToolResultPart result)
            {
                throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a tool message by " +
                    "the OpenAI-compatible request translator.");
            }

            var content = new StringBuilder();

            foreach (var resultPart in result.Content)
            {
                _ = resultPart switch
                {
                    TextPart text => content.Append(text.Text),
                    StructuredDataPart structuredData => content.Append(structuredData.Value.GetRawText()),
                    _ => throw new NotSupportedException(
                                                $"Tool result content part kind '{resultPart.GetType().Name}' is not " +
                                                "supported by the OpenAI-compatible request translator."),
                };
            }

            yield return new JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = providerCallIds.GetValueOrDefault(result.CallId, result.CallId.ToString()),
                ["content"] = content.ToString(),
            };
        }
    }

    private static JsonArray TranslateTools(ImmutableArray<LlmToolDefinition> tools)
    {
        var result = new JsonArray();

        foreach (var tool in tools)
        {
            var function = new JsonObject { ["name"] = tool.Name };

            if (tool.Description is { } description)
            {
                function["description"] = description;
            }

            function["parameters"] = JsonNode.Parse(tool.ParametersSchema.GetRawText());

            result.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = function,
            });
        }

        return result;
    }

    private static JsonNode TranslateToolChoice(LlmToolChoice toolChoice) =>
        toolChoice.Mode switch
        {
            LlmToolChoiceMode.Auto => JsonValue.Create("auto"),
            LlmToolChoiceMode.None => JsonValue.Create("none"),
            LlmToolChoiceMode.Required => JsonValue.Create("required"),
            LlmToolChoiceMode.Named => new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = toolChoice.ForcedToolName },
            },
            _ => throw new NotSupportedException(
                $"Tool choice mode '{toolChoice.Mode}' is not supported by the OpenAI-compatible " +
                "request translator."),
        };
}
