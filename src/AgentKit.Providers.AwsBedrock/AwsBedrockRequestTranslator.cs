// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// The default <see cref="IAwsBedrockRequestTranslator"/>, covering the
/// message roles, content blocks, tool definitions, tool choice, and
/// sampling settings supported by the Bedrock Converse wire format.
/// </summary>
/// <remarks>
/// <para>
/// Bedrock's <c>ToolConfiguration</c> exposes only <c>tools</c> and
/// <c>toolChoice</c>; there is no field, and no documented
/// <c>additionalModelRequestFields</c> convention, that toggles parallel
/// tool calling. A request that leaves
/// <see cref="LlmRequestSettings.ParallelToolCalls"/> unset or
/// sets it to <see langword="true"/> is therefore translated with no
/// wire-format change. A request that sets it to <see langword="false"/>
/// cannot be honored, so translation throws
/// <see cref="NotSupportedException"/> rather than silently ignoring the
/// caller's requirement.
/// </para>
/// <para>
/// Bedrock's <c>ToolChoice</c> is a union of <c>auto</c>, <c>any</c>, and
/// <c>tool</c>; it has no member meaning "the model must not call any
/// tool". Because this translator always keeps <c>tools</c> declared when
/// the caller supplied any (a prior turn's <c>toolUse</c>/<c>toolResult</c>
/// content blocks may still need to be represented, and Bedrock's
/// <c>Converse</c> validation is not guaranteed to accept those blocks
/// without a matching <c>toolConfig</c>), a request for
/// <see cref="LlmToolChoiceMode.None"/> while tools are present cannot be
/// honestly represented: omitting <c>toolChoice</c> falls back to
/// Bedrock's implicit <c>auto</c> default, leaving the model free to call
/// the still-declared tools. Translation therefore throws
/// <see cref="NotSupportedException"/> for <see cref="LlmToolChoiceMode.None"/>
/// instead of silently downgrading it to <c>auto</c>.
/// </para>
/// </remarks>
public sealed class AwsBedrockRequestTranslator: IAwsBedrockRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(LlmModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var providerCallIds = ProviderToolCallIds.Collect(context.Messages);
        var system = new JsonArray();

        var body = new JsonObject
        {
            ["messages"] = TranslateMessages(context.Messages, providerCallIds, system),
        };

        if (system.Count > 0)
        {
            body["system"] = system;
        }

        if (context.Tools.Length > 0)
        {
            body["toolConfig"] = TranslateToolConfig(context.Tools, context.ToolChoice);
        }

        var inferenceConfig = TranslateInferenceConfig(context.Settings);
        if (inferenceConfig.Count > 0)
        {
            body["inferenceConfig"] = inferenceConfig;
        }

        if (context.Settings.Seed is not null)
        {
            throw new NotSupportedException(
                "The Bedrock Converse API does not support a deterministic sampling seed.");
        }

        if (context.Settings.ParallelToolCalls is false)
        {
            throw new NotSupportedException(
                "Settings.ParallelToolCalls=false is not supported by the Bedrock Converse API: " +
                "ToolConfiguration exposes only 'tools' and 'toolChoice', with no control to forbid " +
                "parallel tool use.");
        }

        ProviderJson.ApplyExtensions(body, context.Settings.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static JsonObject TranslateInferenceConfig(LlmRequestSettings settings)
    {
        var inferenceConfig = new JsonObject();

        if (settings.MaxOutputTokens is { } maxOutputTokens)
        {
            inferenceConfig["maxTokens"] = maxOutputTokens;
        }

        if (settings.Temperature is { } temperature)
        {
            inferenceConfig["temperature"] = temperature;
        }

        if (settings.TopP is { } topP)
        {
            inferenceConfig["topP"] = topP;
        }

        if (settings.StopSequences.Length > 0)
        {
            var stop = new JsonArray();
            foreach (var stopSequence in settings.StopSequences)
            {
                stop.Add(JsonValue.Create(stopSequence));
            }

            inferenceConfig["stopSequences"] = stop;
        }

        return inferenceConfig;
    }

    private static JsonArray TranslateMessages(
        ImmutableArray<AgentMessage> messages,
        Dictionary<ToolCallId, string> providerCallIds,
        JsonArray system)
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
                    AppendSystemContent(system, message.Parts);
                    break;

                case UserMessage:
                    result.Add(CreateMessage("user", TranslateUserContent(message.Parts)));
                    break;

                case RuntimeMessage:
                    result.Add(CreateMessage("user", TranslateRuntimeContent(message.Parts)));
                    break;

                case AssistantMessage:
                    result.Add(CreateMessage("assistant", TranslateAssistantContent(message.Parts, providerCallIds)));
                    break;

                case ToolMessage:
                    result.Add(CreateMessage("user", TranslateToolResultContent(message.Parts, providerCallIds)));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Message kind '{message.GetType().Name}' is not supported by the Bedrock Converse " +
                        "request translator.");
            }
        }

        return result;
    }

    private static void AppendSystemContent(JsonArray system, ImmutableArray<ContentPart> parts)
    {
        foreach (var part in parts)
        {
            if (part is not TextPart text)
            {
                throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a system or developer " +
                    "message by the Bedrock Converse request translator.");
            }

            system.Add(new JsonObject { ["text"] = text.Text });
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
                    content.Add(new JsonObject { ["text"] = text.Text });
                    break;

                case MediaReferencePart:
                    throw new NotSupportedException(
                        "Image, document, and video input is not yet supported by the Bedrock Converse " +
                        "request translator.");

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in a user message by " +
                        "the Bedrock Converse request translator.");
            }
        }

        return content;
    }

    private static JsonArray TranslateRuntimeContent(ImmutableArray<ContentPart> parts) =>
        [new JsonObject { ["text"] = RuntimeMessageProjection.BuildEnvelopeJson(parts) }];

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
                    content.Add(new JsonObject { ["text"] = text.Text });
                    break;

                case ToolCallPart toolCall:
                    content.Add(new JsonObject
                    {
                        ["toolUse"] = new JsonObject
                        {
                            ["toolUseId"] = providerCallIds.GetValueOrDefault(toolCall.CallId, toolCall.CallId.ToString()),
                            ["name"] = toolCall.Tool.Name,
                            ["input"] = JsonNode.Parse(toolCall.Arguments.GetRawText()),
                        },
                    });
                    break;

                case ReasoningPart:
                    throw new NotSupportedException(
                        "Reasoning content is not yet supported by the Bedrock Converse request translator.");

                default:
                    throw new NotSupportedException(
                        $"Content part kind '{part.GetType().Name}' is not supported in an assistant message " +
                        "by the Bedrock Converse request translator.");
            }
        }

        return content;
    }

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
                    $"Content part kind '{part.GetType().Name}' is not supported in a tool message by the " +
                    "Bedrock Converse request translator.");
            }

            content.Add(new JsonObject
            {
                ["toolResult"] = new JsonObject
                {
                    ["toolUseId"] = providerCallIds.GetValueOrDefault(result.CallId, result.CallId.ToString()),
                    ["content"] = TranslateToolResultBlocks(result.Content),
                    ["status"] = result.Outcome.Kind == ToolCallOutcomeKind.Success ? "success" : "error",
                },
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
                    blocks.Add(new JsonObject { ["text"] = text.Text });
                    break;

                case StructuredDataPart structuredData:
                    blocks.Add(new JsonObject { ["json"] = JsonNode.Parse(structuredData.Value.GetRawText()) });
                    break;

                default:
                    throw new NotSupportedException(
                        $"Tool result content part kind '{part.GetType().Name}' is not supported by the " +
                        "Bedrock Converse request translator.");
            }
        }

        return blocks;
    }

    private static JsonObject TranslateToolConfig(ImmutableArray<LlmToolDefinition> tools, LlmToolChoice toolChoice) =>
        toolChoice.Mode == LlmToolChoiceMode.None
            ? throw new NotSupportedException(
                "Tool choice 'None' is not supported by the Bedrock Converse API while tools are declared: " +
                "ToolChoice is a union of 'auto', 'any', and 'tool' with no member meaning the model must " +
                "not call any tool, so a request that declares 'tools' cannot honestly forbid their use.")
            : new JsonObject
            {
                ["tools"] = TranslateTools(tools),
                ["toolChoice"] = TranslateToolChoice(toolChoice),
            };

    private static JsonArray TranslateTools(ImmutableArray<LlmToolDefinition> tools)
    {
        var result = new JsonArray();

        foreach (var tool in tools)
        {
            var toolSpec = new JsonObject
            {
                ["name"] = tool.Name,
                ["inputSchema"] = new JsonObject
                {
                    ["json"] = JsonNode.Parse(tool.ParametersSchema.GetRawText()),
                },
            };

            if (tool.Description is { } description)
            {
                toolSpec["description"] = description;
            }

            result.Add(new JsonObject { ["toolSpec"] = toolSpec });
        }

        return result;
    }

    private static JsonObject TranslateToolChoice(LlmToolChoice toolChoice) =>
        toolChoice.Mode switch
        {
            LlmToolChoiceMode.Auto => new JsonObject { ["auto"] = new JsonObject() },
            LlmToolChoiceMode.Required => new JsonObject { ["any"] = new JsonObject() },
            LlmToolChoiceMode.Named => new JsonObject
            {
                ["tool"] = new JsonObject { ["name"] = toolChoice.ForcedToolName },
            },
            LlmToolChoiceMode.None => throw new NotSupportedException(
                "Tool choice 'None' is handled by TranslateToolConfig and never reaches this method."),
            _ => throw new NotSupportedException(
                $"Tool choice mode '{toolChoice.Mode}' is not supported by the Bedrock Converse request " +
                "translator."),
        };
}
