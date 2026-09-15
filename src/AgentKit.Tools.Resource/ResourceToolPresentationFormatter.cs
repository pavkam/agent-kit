// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

using System.Diagnostics.CodeAnalysis;

/// <summary>Formats configured-resource calls and projections as bounded literal application content.</summary>
/// <remarks>
/// The formatter omits catalog and content fingerprints and never exposes the host-private path behind a public
/// resource identity. Loaded content remains data even when its media type admits a syntax-language hint.
/// </remarks>
public sealed class ResourceToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => ResourceTool.PresentationDescriptor;

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
                "Resource request exceeds the presentation input limit.",
                bounds,
                ToolPresentationDisposition.Truncated,
                minimumOmittedCharacters: 1)
            : !TryString(arguments, "action", out var action)
            || arguments.EnumerateObject().Any(static property => property.Name is not ("action" or "id"))
                ? Malformed(bounds, "request")
                : action switch
                {
                    "list" when IsNullOrMissing(arguments, "id") =>
                        SafeMessage("List configured resources", bounds, ToolPresentationDisposition.Formatted),
                    "read" when TryString(arguments, "id", out var id) =>
                        SafeMessage($"Read resource: {id}", bounds, ToolPresentationDisposition.Formatted),
                    _ => Malformed(bounds, "request"),
                };

    private static ToolPresentation FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        return result.Outcome.Kind != ToolCallOutcomeKind.Success
            ? SafeMessage(
                $"Resource failed: {result.Outcome.FailureReason ?? result.Outcome.SourceStatus.ToString()}",
                bounds,
                ToolPresentationDisposition.Formatted)
            : TryPayload(result, bounds, out var root)
            ? root.TryGetProperty("resources", out var resources)
                ? FormatList(root, resources, bounds)
                : FormatRead(root, bounds)
            : Malformed(bounds, "result");
    }

    private static ToolPresentation FormatList(
        JsonElement root,
        JsonElement resources,
        ToolPresentationBounds bounds)
    {
        if (!TryString(root, "catalog_version", out _)
            || resources.ValueKind != JsonValueKind.Array)
        {
            return Malformed(bounds, "result");
        }

        var text = new StringBuilder("Configured resources");
        foreach (var resource in resources.EnumerateArray())
        {
            if (!TryString(resource, "id", out var id)
                || !TryString(resource, "kind", out var kind)
                || !TryString(resource, "trust", out var trust)
                || !TryString(resource, "description", out var description)
                || !TryOptionalString(resource, "media_type", out var mediaType)
                || !TryBoolean(resource, "integrity_pinned", out var integrityPinned))
            {
                return Malformed(bounds, "result");
            }

            _ = text.AppendLine().Append("- ").Append(id).Append(" — ").Append(description)
                .Append(" (").Append(kind).Append(", ").Append(trust);
            if (mediaType is not null)
            {
                _ = text.Append(", ").Append(mediaType);
            }
            if (integrityPinned)
            {
                _ = text.Append(", integrity pinned");
            }
            _ = text.Append(')');
        }

        return SafeMessage(text.ToString(), bounds, ToolPresentationDisposition.Formatted);
    }

    private static ToolPresentation FormatRead(JsonElement root, ToolPresentationBounds bounds)
    {
        if (!TryString(root, "id", out var id)
            || !TryString(root, "kind", out var kind)
            || !TryString(root, "trust", out var trust)
            || !TryOptionalString(root, "media_type", out var mediaType)
            || !TryNonnegativeInt64(root, "bytes", out var bytes)
            || !TryBoolean(root, "instruction_authority", out var instructionAuthority)
            || instructionAuthority
            || !TryBoolean(root, "truncated", out var truncated)
            || !TryString(root, "content", out var content))
        {
            return Malformed(bounds, "result");
        }

        var header = new StringBuilder("Resource ").Append(id)
            .Append(" — ").Append(kind).Append(" / ").Append(trust);
        if (mediaType is not null)
        {
            _ = header.Append(" / ").Append(mediaType);
        }
        _ = header.Append("; ").Append(bytes).Append(" bytes; ")
            .Append(truncated ? "truncated" : "complete")
            .Append("; data only");

        return Bounded(
            [
                new ToolPresentationPart(ToolPresentationPartKind.Text, header.ToString()),
                new ToolPresentationPart(ToolPresentationPartKind.Code, content, LanguageFor(mediaType)),
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
            $"Resource {subject} is malformed and cannot be presented safely.",
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

    private static bool TryOptionalString(JsonElement root, string name, out string? value)
    {
        value = null;
        return !root.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null
            || (property.ValueKind == JsonValueKind.String && (value = property.GetString()) is not null);
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

    private static string? LanguageFor(string? mediaType) => mediaType switch
    {
        "application/json" => "json",
        "application/xml" or "text/xml" => "xml",
        "application/yaml" or "text/yaml" => "yaml",
        "text/markdown" => "markdown",
        _ => null,
    };
}
