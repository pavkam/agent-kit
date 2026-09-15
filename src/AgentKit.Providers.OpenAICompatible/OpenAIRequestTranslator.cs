// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;

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
        var providerCallIds = ProviderToolCallIds.Collect(context.Messages);

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["messages"] = TranslateMessages(context.Messages, profile, providerCallIds),
            // This adapter is a single-candidate operation: the parser reads exactly one choice and rejects extra
            // ones. Pinning `n` here makes it a translator-owned field, so an extension value cannot request more
            // candidates than the response contract can represent.
            ["n"] = 1,
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

        if (context.Settings.ReasoningEffort is { } reasoningEffort)
        {
            body["reasoning_effort"] = reasoningEffort switch
            {
                LlmReasoningEffort.Low => "low",
                LlmReasoningEffort.Medium => "medium",
                LlmReasoningEffort.High => "high",
                LlmReasoningEffort.ExtraHigh => "xhigh",
                LlmReasoningEffort.None => "none",
                _ => throw new UnreachableException($"Unknown reasoning effort '{reasoningEffort}'."),
            };
        }

        ProviderJson.ApplyExtensions(body, context.Settings.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
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
                    result.Add(CreateTextMessage("user", new JsonObject
                    {
                        ["format"] = "agentkit.runtime-message.v1",
                        ["content"] = JoinText(message.Parts),
                    }.ToJsonString()));
                    break;

                case AssistantMessage:
                    result.Add(TranslateAssistantMessage(message.Parts, providerCallIds, profile.AssistantReasoningReplay));
                    break;

                case ToolMessage:
                    foreach (var toolResultMessage in TranslateToolMessage(message.Parts, providerCallIds, profile))
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

    /// <summary>Projects one complete assistant turn onto the wire, replaying reasoning per the profile's dialect.</summary>
    /// <param name="parts">The assistant message's ordered content parts.</param>
    /// <param name="providerCallIds">The provider call identifiers collected for every tool call in the history.</param>
    /// <param name="reasoningReplay">The defined replay mode selected by the compatibility profile.</param>
    /// <returns>The assistant wire message carrying <c>content</c>, optional <c>tool_calls</c>, and optional <c>reasoning_content</c>.</returns>
    private static JsonObject TranslateAssistantMessage(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds,
        OpenAIAssistantReasoningReplay reasoningReplay)
    {
        Debug.Assert(providerCallIds is not null, "The caller collects provider call identifiers before translating messages.");
        Debug.Assert(Enum.IsDefined(reasoningReplay), "The compatibility profile validates its replay mode on assignment.");
        var text = new StringBuilder();
        StringBuilder? reasoning = null;
        JsonArray? toolCalls = null;

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart textPart:
                    _ = text.Append(textPart.Text);
                    break;

                case ReasoningPart reasoningPart:
                    // Reasoning is operational evidence the model produced, never instruction authority. Whether it
                    // returns to the provider is a per-dialect fact: DeepSeek and Kimi thinking models expect
                    // `reasoning_content` echoed within a tool-call loop, while OpenAI rejects unknown members.
                    if (reasoningReplay is OpenAIAssistantReasoningReplay.ReasoningContentField
                        && reasoningPart.Content.Text is { Length: > 0 } reasoningText)
                    {
                        reasoning ??= new StringBuilder();
                        _ = reasoning.Append(reasoningText);
                    }

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

        if (reasoning is not null)
        {
            message["reasoning_content"] = reasoning.ToString();
        }

        if (toolCalls is not null)
        {
            message["tool_calls"] = toolCalls;
        }

        return message;
    }

    private static IEnumerable<JsonObject> TranslateToolMessage(
        ImmutableArray<ContentPart> parts,
        Dictionary<ToolCallId, string> providerCallIds,
        OpenAICompatibilityProfile profile)
    {
        foreach (var part in parts)
        {
            var result = part as ToolResultPart
                ?? throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a tool message by " +
                    "the OpenAI-compatible request translator.");

            yield return new JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = providerCallIds.GetValueOrDefault(result.CallId, result.CallId.ToString()),
                ["content"] = TranslateToolResultContent(result, profile.MaximumToolResultCharacters),
            };
        }
    }

    /// <summary>Encodes projected outcome evidence and ordered content as data under the tool role.</summary>
    /// <param name="result">The validated tool-result projection; never an authoritative execution record.</param>
    /// <param name="maximumCharacters">The positive source-work and serialized-envelope bound.</param>
    /// <returns>A versioned JSON envelope preserving failure, uncertainty, alias, identity, and content boundaries.</returns>
    private static string TranslateToolResultContent(ToolResultPart result, int maximumCharacters)
    {
        Debug.Assert(result is not null, "A tool-result projection was established by the caller.");
        Debug.Assert(maximumCharacters > 0, "The compatibility profile validates its positive bound.");
        var remaining = maximumCharacters;
        var version = result.Tool.Version?.ToString();
        ConsumeToolResultBudget(result.Tool.Id.Value?.Length ?? 0, ref remaining);
        ConsumeToolResultBudget(result.Tool.Name.Length, ref remaining);
        ConsumeToolResultBudget(version?.Length ?? 0, ref remaining);
        ConsumeToolResultBudget(result.Outcome.FailureReason?.Length ?? 0, ref remaining);
        var content = new JsonArray();
        foreach (var part in result.Content)
        {
            // Charge even empty parts before processing so part count cannot evade the bound.
            ConsumeToolResultBudget(32, ref remaining);
            var (kind, text) = part switch
            {
                TextPart value => ("text", value.Text),
                StructuredDataPart value => ("json", value.Value.GetRawText()),
                _ => throw new NotSupportedException(
                    $"Tool result content part kind '{part.GetType().Name}' is not supported by the OpenAI-compatible request translator."),
            };
            ConsumeToolResultBudget(text.Length, ref remaining);
            content.Add(new JsonObject { ["type"] = kind, ["text"] = text });
        }

        var envelope = new JsonObject
        {
            ["format"] = "agentkit.tool-result.v1",
            ["tool"] = new JsonObject
            {
                ["id"] = result.Tool.Id.Value,
                ["version"] = version,
                ["requested_name"] = result.Tool.Name,
            },
            ["outcome"] = new JsonObject
            {
                ["kind"] = result.Outcome.Kind.ToString(),
                ["source_status"] = (int) result.Outcome.SourceStatus,
                ["source_status_name"] = Enum.GetName(result.Outcome.SourceStatus),
                ["side_effect_certainty"] = result.Outcome.SideEffectCertainty.ToString(),
                ["retryable"] = result.Outcome.Retryable,
                ["failure_reason"] = result.Outcome.FailureReason,
            },
            ["content"] = content,
        }.ToJsonString();
        return envelope.Length <= maximumCharacters
            ? envelope
            : throw new NotSupportedException("The tool-result envelope exceeds the compatibility profile's character bound.");
    }

    /// <summary>Charges source text and per-part work before allocating a translated envelope.</summary>
    /// <param name="characters">The nonnegative charge established from a string or fixed part cost.</param>
    /// <param name="remaining">The nonnegative remaining profile budget, reduced only on success.</param>
    private static void ConsumeToolResultBudget(int characters, ref int remaining)
    {
        Debug.Assert(characters >= 0 && remaining >= 0, "Source lengths and remaining budget are nonnegative.");
        if (characters > remaining)
        {
            throw new NotSupportedException("The tool-result projection exceeds the compatibility profile's character bound.");
        }

        remaining -= characters;
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
