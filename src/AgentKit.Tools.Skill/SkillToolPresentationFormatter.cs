// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

using System.Diagnostics.CodeAnalysis;

/// <summary>Formats captured-skill discovery and activation as bounded literal application content.</summary>
/// <remarks>
/// Catalog and content fingerprints are deliberately omitted. Activated skill text is emitted as a code part so an
/// application can preserve and select the exact non-authoritative content without interpreting it as UI markup.
/// </remarks>
public sealed class SkillToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => SkillTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(
        ToolPresentationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(request.Source switch
        {
            ToolCallPresentationSource call => FormatCall(call.Call.Arguments, request.Bounds),
            ToolResultPresentationSource result => FormatResult(result.Result, request.Bounds),
            _ => null,
        });
    }

    private static ToolPresentation FormatCall(JsonElement arguments, ToolPresentationBounds bounds)
        => Encoding.UTF8.GetByteCount(arguments.GetRawText()) > bounds.MaximumInputBytes
            ? SafeMessage(
                "Skill request exceeds the presentation input limit.",
                bounds,
                ToolPresentationDisposition.Truncated,
                minimumOmittedCharacters: 1)
            : !TryString(arguments, "action", out var action)
            || arguments.EnumerateObject().Any(static property => property.Name is not ("action" or "id"))
                ? Malformed(bounds, "request")
                : action switch
                {
                    "list" when IsNullOrMissing(arguments, "id") =>
                        SafeMessage("List available skills", bounds, ToolPresentationDisposition.Formatted),
                    "activate" when TryString(arguments, "id", out var id) =>
                        SafeMessage($"Activate skill: {id}", bounds, ToolPresentationDisposition.Formatted),
                    _ => Malformed(bounds, "request"),
                };

    private static ToolPresentation FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        return result.Outcome.Kind != ToolCallOutcomeKind.Success
            ? SafeMessage(
                $"Skill failed: {result.Outcome.FailureReason ?? result.Outcome.SourceStatus.ToString()}",
                bounds,
                ToolPresentationDisposition.Formatted)
            : TryPayload(result, bounds, out var root)
            ? root.TryGetProperty("skills", out var skills)
                ? FormatList(root, skills, bounds)
                : FormatActivation(root, bounds)
            : Malformed(bounds, "result");
    }

    private static ToolPresentation FormatList(JsonElement root, JsonElement skills, ToolPresentationBounds bounds)
    {
        if (!TryString(root, "catalog_version", out _)
            || !TryBoolean(root, "instruction_authority", out var instructionAuthority)
            || instructionAuthority
            || skills.ValueKind != JsonValueKind.Array)
        {
            return Malformed(bounds, "result");
        }

        var text = new StringBuilder("Available skills");
        foreach (var skill in skills.EnumerateArray())
        {
            if (!TryString(skill, "id", out var id)
                || !TryString(skill, "Name", out var name)
                || !TryString(skill, "Description", out var description)
                || !TryString(skill, "trust", out var trust))
            {
                return Malformed(bounds, "result");
            }

            _ = text.AppendLine().Append("- ").Append(id).Append(" — ").Append(name)
                .Append(" [").Append(trust).AppendLine("]")
                .Append("  ").Append(description);
        }

        return SafeMessage(text.ToString(), bounds, ToolPresentationDisposition.Formatted);
    }

    private static ToolPresentation FormatActivation(JsonElement root, ToolPresentationBounds bounds)
    {
        if (!TryString(root, "catalog_version", out _)
            || !TryString(root, "id", out var id)
            || !TryString(root, "Name", out var name)
            || !TryString(root, "Description", out var description)
            || !TryString(root, "trust", out var trust)
            || !TryNonnegativeInt64(root, "bytes", out var bytes)
            || !TryBoolean(root, "truncated", out var truncated)
            || !TryBoolean(root, "instruction_authority", out var instructionAuthority)
            || instructionAuthority
            || !TryString(root, "content", out var content))
        {
            return Malformed(bounds, "result");
        }

        var header = $"Skill {id} — {name} [{trust}]; {bytes} bytes; "
            + $"{(truncated ? "truncated" : "complete")}; data only\n{description}";
        return Bounded(
            [
                new ToolPresentationPart(ToolPresentationPartKind.Text, header),
                new ToolPresentationPart(ToolPresentationPartKind.Code, content, "markdown"),
            ],
            bounds,
            truncated ? ToolPresentationDisposition.Truncated : ToolPresentationDisposition.Formatted,
            truncated ? 1 : 0);
    }

    private static bool TryPayload(
        ToolResultPart result,
        ToolPresentationBounds bounds,
        out JsonElement root)
    {
        root = default;
        if (result.Content.Length != 1
            || result.Content[0] is not TextPart text
            || Encoding.UTF8.GetByteCount(text.Text) > bounds.MaximumInputBytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text.Text);
            root = document.RootElement.Clone();
            return root.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ToolPresentation Malformed(ToolPresentationBounds bounds, string subject) =>
        SafeMessage(
            $"Skill {subject} is malformed and cannot be presented safely.",
            bounds,
            ToolPresentationDisposition.Fallback);

    private static ToolPresentation SafeMessage(
        string text,
        ToolPresentationBounds bounds,
        ToolPresentationDisposition disposition,
        long minimumOmittedCharacters = 0) =>
        Bounded(
            [new ToolPresentationPart(ToolPresentationPartKind.Text, text)],
            bounds,
            disposition,
            minimumOmittedCharacters);

    private static ToolPresentation Bounded(
        ImmutableArray<ToolPresentationPart> parts,
        ToolPresentationBounds bounds,
        ToolPresentationDisposition disposition,
        long minimumOmittedCharacters = 0)
    {
        var output = ImmutableArray.CreateBuilder<ToolPresentationPart>(Math.Min(parts.Length, bounds.MaximumParts));
        var remaining = bounds.MaximumOutputCharacters;
        var omitted = minimumOmittedCharacters;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (index >= bounds.MaximumParts || remaining == 0)
            {
                omitted += RemainingCharacters(parts, index);
                break;
            }

            var text = Take(part.Text, ref remaining, ref omitted);
            var language = part.Language is { } value ? Take(value, ref remaining, ref omitted) : null;
            output.Add(new ToolPresentationPart(part.Kind, text, string.IsNullOrEmpty(language) ? null : language, part.Path));
        }

        return new ToolPresentation(
            output.ToImmutable(),
            omitted > 0 ? ToolPresentationDisposition.Truncated : disposition,
            omitted);
    }

    private static long RemainingCharacters(ImmutableArray<ToolPresentationPart> parts, int start)
    {
        long characters = 0;
        for (var index = start; index < parts.Length; index++)
        {
            characters += parts[index].Text.Length + (parts[index].Language?.Length ?? 0) + (parts[index].Path?.Length ?? 0);
        }
        return characters;
    }

    private static string Take(string value, ref int remaining, ref long omitted)
    {
        var builder = new StringBuilder(Math.Min(value.Length, remaining));
        var consumed = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Utf16SequenceLength > remaining)
            {
                break;
            }
            _ = builder.Append(rune);
            remaining -= rune.Utf16SequenceLength;
            consumed += rune.Utf16SequenceLength;
        }
        omitted += value.Length - consumed;
        return builder.ToString();
    }

    private static bool IsNullOrMissing(JsonElement root, string name) =>
        !root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null;

    private static bool TryString(JsonElement root, string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            && (value = property.GetString()) is not null;
    }

    private static bool TryBoolean(JsonElement root, string name, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = property.GetBoolean();
        return true;
    }

    private static bool TryNonnegativeInt64(JsonElement root, string name, out long value)
    {
        value = 0;
        return root.TryGetProperty(name, out var property)
            && property.TryGetInt64(out value)
            && value >= 0;
    }
}
