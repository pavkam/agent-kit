// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit;

/// <summary>Formats exact edit arguments as a literal replacement preview and result projection.</summary>
public sealed class EditToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => EditTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Source is ToolCallPresentationSource call)
        {
            var root = call.Call.Arguments;
            if (root.ValueKind != JsonValueKind.Object
                || !String(root, "path", out var path)
                || string.IsNullOrWhiteSpace(path)
                || !String(root, "old_text", out var oldText)
                || string.IsNullOrEmpty(oldText)
                || !String(root, "new_text", out var newText))
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            if (root.TryGetProperty("replace_all", out var replaceAll)
                && replaceAll.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            var scope = replaceAll.ValueKind == JsonValueKind.True
                ? "Replace every exact match"
                : "Replace one exact match; fail if there are multiple matches";
            var diff = $"-{oldText.Replace("\n", "\n-", StringComparison.Ordinal)}\n+{newText!.Replace("\n", "\n+", StringComparison.Ordinal)}";
            return ValueTask.FromResult<ToolPresentation?>(new ToolPresentation(
                [
                    new ToolPresentationPart(ToolPresentationPartKind.Text, scope),
                    new ToolPresentationPart(ToolPresentationPartKind.Diff, diff, path: path),
                ], ToolPresentationDisposition.Formatted));
        }
        return ValueTask.FromResult(request.Source is ToolResultPresentationSource result
            ? (ToolPresentation?) FormatResult(result.Result, request.Bounds)
            : null);
    }

    private static ToolPresentation FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        if (result.Outcome.Kind != ToolCallOutcomeKind.Success)
        {
            return Bounded(
                $"Edit failed: {result.Outcome.FailureReason ?? result.Outcome.SourceStatus.ToString()}",
                bounds);
        }

        var projection = result.Content.OfType<TextPart>().FirstOrDefault()?.Text;
        if (projection is null)
        {
            return Bounded("Edit completed, but its result projection is unavailable.", bounds,
                ToolPresentationDisposition.Fallback);
        }

        if (Encoding.UTF8.GetByteCount(projection) > bounds.MaximumInputBytes)
        {
            var text = "Edit completed, but its result projection exceeds the presentation input limit.";
            return new ToolPresentation(
                [new ToolPresentationPart(ToolPresentationPartKind.Text,
                    text[..Math.Min(text.Length, bounds.MaximumOutputCharacters)])],
                ToolPresentationDisposition.Truncated,
                1);
        }

        try
        {
            using var document = JsonDocument.Parse(projection);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !String(root, "status", out var status)
                || string.IsNullOrWhiteSpace(status)
                || !String(root, "path", out var path)
                || string.IsNullOrWhiteSpace(path)
                || !root.TryGetProperty("replacements", out var replacements)
                || !replacements.TryGetInt32(out var replacementCount)
                || replacementCount < 0
                || !root.TryGetProperty("bytes", out var bytes)
                || !bytes.TryGetInt64(out var byteCount)
                || byteCount < 0
                || !root.TryGetProperty("atomic_target_visibility", out var atomicVisibility)
                || atomicVisibility.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return Bounded("Edit completed, but its result projection is malformed.", bounds,
                    ToolPresentationDisposition.Fallback);
            }

            string? warning = null;
            if (root.TryGetProperty("warning", out var warningProperty))
            {
                if (warningProperty.ValueKind == JsonValueKind.String)
                {
                    warning = warningProperty.GetString();
                }
                else if (warningProperty.ValueKind != JsonValueKind.Null)
                {
                    return Bounded("Edit completed, but its result projection is malformed.", bounds,
                        ToolPresentationDisposition.Fallback);
                }
            }

            var text = $"Edited: {path}\nStatus: {status}\nReplacements: {replacementCount}\nBytes: {byteCount}\nAtomic target visibility: {(atomicVisibility.GetBoolean() ? "yes" : "no")}";
            if (!string.IsNullOrWhiteSpace(warning))
            {
                text += $"\nWarning: {warning}";
            }
            return Bounded(text, bounds);
        }
        catch (JsonException)
        {
            return Bounded("Edit completed, but its result projection is malformed.", bounds,
                ToolPresentationDisposition.Fallback);
        }
    }

    private static ToolPresentation Bounded(
        string text,
        ToolPresentationBounds bounds,
        ToolPresentationDisposition disposition = ToolPresentationDisposition.Formatted)
    {
        var maximum = bounds.MaximumOutputCharacters;
        return text.Length <= maximum
            ? new ToolPresentation([new ToolPresentationPart(ToolPresentationPartKind.Text, text)], disposition)
            : new ToolPresentation(
                [new ToolPresentationPart(ToolPresentationPartKind.Text, text[..maximum])],
                ToolPresentationDisposition.Truncated,
                text.Length - maximum);
    }

    private static bool String(JsonElement root, string name, out string? value)
    {
        System.Diagnostics.Debug.Assert(root.ValueKind == JsonValueKind.Object, "Call parsing validates the argument object.");
        if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }
        value = null;
        return false;
    }
}
