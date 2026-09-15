// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// The default <see cref="IAnthropicMessageTranslator"/>, covering the
/// message roles, content blocks, tool definitions, tool choice, and
/// sampling settings supported by the Anthropic Messages wire format.
/// </summary>
public sealed class AnthropicMessageTranslator: IAnthropicMessageTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(LlmModelRequest request, AnthropicProviderOptions options, bool useStreaming)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        var context = request.Context;
        var providerCallIds = ProviderToolCallIds.Collect(context.Messages);
        var system = new StringBuilder();

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["max_tokens"] = ResolveMaxOutputTokens(context, options),
            ["messages"] = TranslateMessages(context.Messages, providerCallIds, system),
            ["stream"] = useStreaming,
        };

        if (system.Length > 0)
        {
            body["system"] = system.ToString();
        }

        if (context.Tools.Length > 0)
        {
            body["tools"] = TranslateTools(context.Tools);
            body["tool_choice"] = TranslateToolChoice(context.ToolChoice, context.Settings.ParallelToolCalls);
        }

        if (context.Settings.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        if (context.Settings.TopP is { } topP)
        {
            body["top_p"] = topP;
        }

        if (context.Settings.StopSequences.Length > 0)
        {
            var stop = new JsonArray();
            foreach (var stopSequence in context.Settings.StopSequences)
            {
                stop.Add(JsonValue.Create(stopSequence));
            }

            body["stop_sequences"] = stop;
        }

        if (context.Settings.Seed is not null)
        {
            throw new NotSupportedException(
                "The Anthropic Messages API does not support a deterministic sampling seed.");
        }

        ProviderJson.ApplyExtensions(body, context.Settings.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static long ResolveMaxOutputTokens(LlmRequestContext context, AnthropicProviderOptions options) =>
        context.Settings.MaxOutputTokens
        ?? context.Model.Limits.MaxOutputTokens
        ?? options.DefaultMaxOutputTokens;

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
                    result.Add(CreateMessage("user", TranslateUserContent(message.Parts)));
                    break;

                case RuntimeMessage:
                    result.Add(CreateMessage("user", TranslateUserContent(message.Parts)));
                    break;

                case AssistantMessage:
                    result.Add(CreateMessage("assistant", TranslateAssistantContent(message.Parts, providerCallIds)));
                    break;

                case ToolMessage:
                    result.Add(CreateMessage("user", TranslateToolResultContent(message.Parts, providerCallIds)));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Message kind '{message.GetType().Name}' is not supported by the Anthropic " +
                        "Messages request translator.");
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
                    "message by the Anthropic Messages request translator.");
            }

            if (system.Length > 0)
            {
                _ = system.Append("\n\n");
            }

            _ = system.Append(text.Text);
        }
    }

    private static JsonObject CreateMessage(string role, JsonArray content) =>
        new()
        {
            ["role"] = role,
            ["content"] = content,
        };

    private static JsonArray TranslateUserContent(ImmutableArray<ContentPart> parts)
    {
        var content = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                    break;

                case MediaReferencePart:
                    throw new NotSupportedException(
                        "Image and document input is not yet supported by the Anthropic Messages " +
                        "request translator.");

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in a user message " +
                        "by the Anthropic Messages request translator.");
            }
        }

        return content;
    }

    private static JsonArray TranslateAssistantContent(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        var content = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                    break;

                case ToolCallPart toolCall:
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = providerCallIds.GetValueOrDefault(toolCall.CallId, toolCall.CallId.ToString()),
                        ["name"] = toolCall.Tool.Name,
                        ["input"] = JsonNode.Parse(toolCall.Arguments.GetRawText()),
                    });
                    break;

                case ReasoningPart reasoning:
                    content.Add(TranslateReasoning(reasoning));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in an assistant " +
                        "message by the Anthropic Messages request translator.");
            }
        }

        return content;
    }

    private static JsonObject TranslateReasoning(ReasoningPart reasoning) =>
        reasoning.Content.Visibility switch
        {
            ReasoningVisibility.Visible => new JsonObject
            {
                ["type"] = "thinking",
                ["thinking"] = reasoning.Content.Text ?? string.Empty,
                ["signature"] = reasoning.Content.SignatureToken ?? string.Empty,
            },
            ReasoningVisibility.Redacted => new JsonObject
            {
                ["type"] = "redacted_thinking",
                ["data"] = reasoning.Content.SignatureToken
                    ?? throw new NotSupportedException(
                        "A redacted thinking block requires its opaque signature token to echo back to Anthropic."),
            },
            ReasoningVisibility.EncryptedSignature => throw new NotSupportedException(
                "Encrypted reasoning signatures cannot be represented by the Anthropic Messages wire format."),
            _ => throw new NotSupportedException(
                            $"Reasoning visibility '{reasoning.Content.Visibility}' has no Anthropic content-block " +
                            "equivalent."),
        };

    private static JsonArray TranslateToolResultContent(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds)
    {
        var content = new JsonArray();

        foreach (var part in parts)
        {
            if (part is not ToolResultPart result)
            {
                throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a tool message by " +
                    "the Anthropic Messages request translator.");
            }

            content.Add(new JsonObject
            {
                ["type"] = "tool_result",
                ["tool_use_id"] = providerCallIds.GetValueOrDefault(result.CallId, result.CallId.ToString()),
                ["content"] = TranslateToolResultBlocks(result.Content),
                ["is_error"] = result.Outcome.Kind != ToolCallOutcomeKind.Success,
            });
        }

        return content;
    }

    private static JsonArray TranslateToolResultBlocks(ImmutableArray<ContentPart> parts)
    {
        var blocks = new JsonArray();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    blocks.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                    break;

                case StructuredDataPart structuredData:
                    blocks.Add(new JsonObject { ["type"] = "text", ["text"] = structuredData.Value.GetRawText() });
                    break;

                default:
                    throw new NotSupportedException(
                        $"Tool result content part kind '{part.GetType().Name}' is not supported by the " +
                        "Anthropic Messages request translator.");
            }
        }

        return blocks;
    }

    private static JsonArray TranslateTools(ImmutableArray<LlmToolDefinition> tools)
    {
        var result = new JsonArray();

        foreach (var tool in tools)
        {
            var definition = new JsonObject
            {
                ["name"] = tool.Name,
                ["input_schema"] = JsonNode.Parse(tool.ParametersSchema.GetRawText()),
            };

            if (tool.Description is { } description)
            {
                definition["description"] = description;
            }

            result.Add(definition);
        }

        return result;
    }

    private static JsonObject TranslateToolChoice(LlmToolChoice toolChoice, bool? parallelToolCalls)
    {
        var node = toolChoice.Mode switch
        {
            LlmToolChoiceMode.Auto => new JsonObject { ["type"] = "auto" },
            LlmToolChoiceMode.None => new JsonObject { ["type"] = "none" },
            LlmToolChoiceMode.Required => new JsonObject { ["type"] = "any" },
            LlmToolChoiceMode.Named => new JsonObject { ["type"] = "tool", ["name"] = toolChoice.ForcedToolName },
            _ => throw new NotSupportedException(
                $"Tool choice mode '{toolChoice.Mode}' is not supported by the Anthropic Messages request " +
                "translator."),
        };

        if (parallelToolCalls is false && toolChoice.Mode != LlmToolChoiceMode.None)
        {
            node["disable_parallel_tool_use"] = true;
        }

        return node;
    }
}
