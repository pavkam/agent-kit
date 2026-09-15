// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob;

using System.Diagnostics.CodeAnalysis;
using System.Text;

/// <summary>Formats glob call and projected result evidence as bounded provider-neutral literal parts.</summary>
public sealed class GlobToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => GlobTool.PresentationDescriptor;

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

        var (validBasePath, basePath) = OptionalString(arguments, "base_path");
        var exclusions = StringArray(arguments, "exclude_patterns");
        if (!validBasePath || exclusions is null)
        {
            return null;
        }

        var location = basePath is not null ? $" under {basePath}" : "";
        var excluded = exclusions.Count > 0 ? $"; excluding {string.Join(", ", exclusions)}" : "";
        return Presentation($"Glob {pattern}{location}{excluded}");
    }

    private static ToolPresentation? FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        if (!TryPayload(result, bounds, out var root) ||
            !TryString(root, "status", out var status) ||
            !root.TryGetProperty("matches", out var matches) || matches.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("visited_entries", out var visited) || !visited.TryGetInt32(out var visitedCount) ||
            !root.TryGetProperty("complete", out var completeProperty) || completeProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return FailureOnly(result);
        }

        var paths = new List<string>();
        foreach (var match in matches.EnumerateArray())
        {
            if (match.ValueKind != JsonValueKind.String || match.GetString() is not { } path)
            {
                return FailureOnly(result);
            }
            paths.Add(path);
        }

        var complete = completeProperty.GetBoolean();
        var outcome = result.Outcome.Kind == ToolCallOutcomeKind.Success ? status : result.Outcome.FailureReason ?? status;
        var summary = $"{outcome}: {paths.Count} match{(paths.Count == 1 ? "" : "es")}; visited {visitedCount} entr{(visitedCount == 1 ? "y" : "ies")}; {(complete ? "complete" : "incomplete")}.";
        ImmutableArray<ToolPresentationPart> parts = paths.Count == 0
            ? [new ToolPresentationPart(ToolPresentationPartKind.Text, summary)]
            : [new ToolPresentationPart(ToolPresentationPartKind.Text, summary),
                new ToolPresentationPart(ToolPresentationPartKind.Code, string.Join('\n', paths))];
        return new ToolPresentation(parts, ToolPresentationDisposition.Formatted);
    }

    private static ToolPresentation? FailureOnly(ToolResultPart result) =>
        result.Outcome.Kind == ToolCallOutcomeKind.Success
            ? null
            : Presentation(result.Outcome.FailureReason ?? result.Outcome.SourceStatus.ToString());

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
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String && (value = property.GetString()) is not null;
    }

    private static (bool Valid, string? Value) OptionalString(JsonElement root, string name) =>
        !root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null
            ? (true, null)
            : property.ValueKind == JsonValueKind.String && property.GetString() is { } value ? (true, value) : (false, null);

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
