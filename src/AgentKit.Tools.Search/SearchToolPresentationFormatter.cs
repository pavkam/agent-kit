// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search;

using System.Diagnostics.CodeAnalysis;
using System.Text;

/// <summary>Formats search call and projected result evidence as bounded provider-neutral literal parts.</summary>
public sealed class SearchToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => SearchTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(request.Source switch
        {
            ToolCallPresentationSource call => FormatCall(call.Call.Arguments),
            ToolResultPresentationSource result => FormatResult(result.Result, request.Bounds),
            _ => null,
        });
    }

    private static ToolPresentation? FormatCall(JsonElement arguments)
    {
        if (!TryString(arguments, "pattern", out var pattern))
        {
            return null;
        }
        var (validBase, basePath) = OptionalString(arguments, "base_path");
        var (validPath, pathPattern) = OptionalString(arguments, "path_pattern");
        var exclusions = StringArray(arguments, "exclude_patterns");
        if (!validBase || !validPath || exclusions is null)
        {
            return null;
        }
        var regex = !arguments.TryGetProperty("regex", out var regexProperty) || regexProperty.ValueKind == JsonValueKind.True
            ? "regex"
            : regexProperty.ValueKind == JsonValueKind.False ? "literal" : null;
        if (regex is null)
        {
            return null;
        }
        var scope = basePath is not null ? $" under {basePath}" : "";
        var files = pathPattern is not null ? $" in {pathPattern}" : "";
        var excluded = exclusions.Count > 0 ? $"; excluding {string.Join(", ", exclusions)}" : "";
        return Presentation($"Search ({regex}) for {pattern}{scope}{files}{excluded}");
    }

    private static ToolPresentation? FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        if (!TryPayload(result, bounds, out var root) || !TryString(root, "status", out var status) ||
            !root.TryGetProperty("matches", out var matches) || matches.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("visited_files", out var files) || !files.TryGetInt32(out var visitedFiles) ||
            !root.TryGetProperty("visited_bytes", out var bytes) || !bytes.TryGetInt64(out var visitedBytes) ||
            !root.TryGetProperty("complete", out var completeProperty) || completeProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return FailureOnly(result);
        }

        var lines = new List<string>();
        foreach (var match in matches.EnumerateArray())
        {
            if (!TryString(match, "path", out var path) || !match.TryGetProperty("line_number", out var number) ||
                !number.TryGetInt32(out var lineNumber) || !TryString(match, "line_text", out var lineText) ||
                !match.TryGetProperty("line_text_truncated", out var truncated) || truncated.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return FailureOnly(result);
            }
            lines.Add($"{path}:{lineNumber}: {lineText}{(truncated.GetBoolean() ? " … [line truncated]" : "")}");
        }

        var complete = completeProperty.GetBoolean();
        var outcome = result.Outcome.Kind == ToolCallOutcomeKind.Success ? status : result.Outcome.FailureReason ?? status;
        var summary = $"{outcome}: {lines.Count} match{(lines.Count == 1 ? "" : "es")} in {visitedFiles} file{(visitedFiles == 1 ? "" : "s")} ({visitedBytes} bytes visited); {(complete ? "complete" : "incomplete")}.";
        ImmutableArray<ToolPresentationPart> parts = lines.Count == 0
            ? [new ToolPresentationPart(ToolPresentationPartKind.Text, summary)]
            : [new ToolPresentationPart(ToolPresentationPartKind.Text, summary),
                new ToolPresentationPart(ToolPresentationPartKind.Code, string.Join('\n', lines))];
        return new ToolPresentation(parts, ToolPresentationDisposition.Formatted);
    }

    private static ToolPresentation? FailureOnly(ToolResultPart result) =>
        result.Outcome.Kind == ToolCallOutcomeKind.Success ? null : Presentation(result.Outcome.FailureReason ?? result.Outcome.SourceStatus.ToString());

    private static bool TryPayload(ToolResultPart result, ToolPresentationBounds bounds, out JsonElement root)
    {
        root = default;
        if (result.Content.Length != 1 || result.Content[0] is not TextPart text || Encoding.UTF8.GetByteCount(text.Text) > bounds.MaximumInputBytes)
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

    private static bool TryString(JsonElement root, string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && (value = property.GetString()) is not null;
    }

    private static (bool Valid, string? Value) OptionalString(JsonElement root, string name) =>
        !root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null ? (true, null) :
        property.ValueKind == JsonValueKind.String && property.GetString() is { } value ? (true, value) : (false, null);

    private static List<string>? StringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property))
        {
            return [];
        }
        if (property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } value)
            {
                return null;
            }
            values.Add(value);
        }
        return values;
    }

    private static ToolPresentation Presentation(string text) => new(
        [new ToolPresentationPart(ToolPresentationPartKind.Text, text)], ToolPresentationDisposition.Formatted);
}
