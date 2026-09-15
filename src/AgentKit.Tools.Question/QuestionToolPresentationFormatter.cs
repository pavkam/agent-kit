// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question;

using System.Text;

/// <summary>Formats question requests and terminal answer evidence without exposing transport JSON.</summary>
public sealed class QuestionToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => QuestionTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(
        ToolPresentationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(request.Source switch
        {
            ToolCallPresentationSource call => FormatCall(call.Call, request.Bounds),
            ToolResultPresentationSource result => FormatResult(result.Result, request.Bounds),
            _ => null,
        });
    }

    private static ToolPresentation FormatCall(ToolCallPart call, ToolPresentationBounds bounds)
    {
        var arguments = call.Arguments;
        if (Encoding.UTF8.GetByteCount(arguments.GetRawText()) > bounds.MaximumInputBytes)
        {
            return Bounded("Question request exceeds the presentation input limit.", bounds,
                ToolPresentationDisposition.Truncated, minimumOmittedCharacters: 1);
        }

        if (arguments.ValueKind != JsonValueKind.Object
            || !TryNonblankString(arguments, "question", out var prompt)
            || !arguments.TryGetProperty("options", out var options)
            || options.ValueKind != JsonValueKind.Array
            || options.GetArrayLength() is < 2 or > 10
            || !TryOptionalBoolean(arguments, "allow_free_text", out var allowsFreeText)
            || !TryOptionalPositiveInteger(arguments, "timeout_seconds", out var timeoutSeconds))
        {
            return Bounded("Question request is malformed and cannot be presented safely.", bounds,
                ToolPresentationDisposition.Fallback);
        }

        var text = new StringBuilder()
            .AppendLine("Question")
            .AppendLine(prompt)
            .AppendLine()
            .AppendLine("Options");
        var index = 1;
        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object
                || !TryNonblankString(option, "id", out var id)
                || !TryNonblankString(option, "label", out var label)
                || !TryNonblankString(option, "description", out var description))
            {
                return Bounded("Question request is malformed and cannot be presented safely.", bounds,
                    ToolPresentationDisposition.Fallback);
            }

            _ = text.Append(index++).Append(". ").Append(label).Append(" [").Append(id).AppendLine("]")
                .Append("   ").AppendLine(description);
        }

        _ = text.AppendLine()
            .Append("Free-text answer: ").AppendLine(allowsFreeText ? "allowed" : "not allowed")
            .Append("Response deadline: ")
            .Append(timeoutSeconds is { } seconds ? $"{seconds} seconds" : "host default");
        return Bounded(text.ToString(), bounds, ToolPresentationDisposition.Formatted);
    }

    private static ToolPresentation FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        if (result.Outcome.Kind != ToolCallOutcomeKind.Success)
        {
            var reason = result.Outcome.FailureReason ?? result.Outcome.SourceStatus.ToString();
            return Bounded($"Question failed: {reason}", bounds, ToolPresentationDisposition.Formatted);
        }

        var projection = result.Content.OfType<TextPart>().FirstOrDefault();
        if (projection is null || Encoding.UTF8.GetByteCount(projection.Text) > bounds.MaximumInputBytes)
        {
            return Bounded("Question result exceeds the presentation input limit.", bounds,
                ToolPresentationDisposition.Truncated, minimumOmittedCharacters: 1);
        }

        try
        {
            using var document = JsonDocument.Parse(projection.Text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryNonblankString(root, "selected_option_id", out var selectedId)
                || !TryNonblankString(root, "selected_option_label", out var selectedLabel))
            {
                return Bounded("Question result is malformed and cannot be presented safely.", bounds,
                    ToolPresentationDisposition.Fallback);
            }

            string? freeText = null;
            if (root.TryGetProperty("free_text", out var freeTextProperty))
            {
                if (freeTextProperty.ValueKind == JsonValueKind.String)
                {
                    freeText = freeTextProperty.GetString();
                }
                else if (freeTextProperty.ValueKind != JsonValueKind.Null)
                {
                    return Bounded("Question result is malformed and cannot be presented safely.", bounds,
                        ToolPresentationDisposition.Fallback);
                }
            }

            var text = new StringBuilder("Selected answer: ")
                .Append(selectedLabel).Append(" [").Append(selectedId).Append(']');
            if (!string.IsNullOrEmpty(freeText))
            {
                _ = text.AppendLine().Append("Free text: ").Append(freeText);
            }
            return Bounded(text.ToString(), bounds, ToolPresentationDisposition.Formatted);
        }
        catch (JsonException)
        {
            return Bounded("Question result is malformed and cannot be presented safely.", bounds,
                ToolPresentationDisposition.Fallback);
        }
    }

    private static ToolPresentation Bounded(
        string text,
        ToolPresentationBounds bounds,
        ToolPresentationDisposition completeDisposition,
        long minimumOmittedCharacters = 0)
    {
        var maximum = bounds.MaximumOutputCharacters;
        return text.Length <= maximum
            ? new ToolPresentation(
                [new ToolPresentationPart(ToolPresentationPartKind.Text, text)],
                completeDisposition,
                minimumOmittedCharacters)
            : new ToolPresentation(
                [new ToolPresentationPart(ToolPresentationPartKind.Text, text[..maximum])],
                ToolPresentationDisposition.Truncated,
                Math.Max(minimumOmittedCharacters, text.Length - maximum));
    }

    private static bool TryNonblankString(JsonElement value, string name, out string result)
    {
        result = string.Empty;
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        result = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(result);
    }

    private static bool TryOptionalBoolean(JsonElement value, string name, out bool result)
    {
        if (!value.TryGetProperty(name, out var property))
        {
            result = false;
            return true;
        }
        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            result = property.GetBoolean();
            return true;
        }
        result = false;
        return false;
    }

    private static bool TryOptionalPositiveInteger(JsonElement value, string name, out long? result)
    {
        if (!value.TryGetProperty(name, out var property))
        {
            result = null;
            return true;
        }
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number) && number > 0)
        {
            result = number;
            return true;
        }
        result = null;
        return false;
    }
}
