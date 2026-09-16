// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

/// <summary>Formats plan call and projected result evidence as bounded provider-neutral literal parts.</summary>
public sealed class PlanToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => PlanTool.PresentationDescriptor;

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
        return !TryString(arguments, "action", out var action) ? null : action switch
        {
            "get" => Presentation("Get current plan"),
            "replace" => ReplacePreview(arguments),
            "set_status" => StatusPreview(arguments),
            _ => null,
        };
    }

    private static ToolPresentation? ReplacePreview(JsonElement arguments)
    {
        if (!TryString(arguments, "title", out var title) || !arguments.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var (validRevision, revision) = Revision(arguments);
        return validRevision
            ? Presentation($"Replace plan “{title}” with {items.GetArrayLength()} item{(items.GetArrayLength() == 1 ? "" : "s")}; expected revision {revision}.")
            : null;
    }

    private static ToolPresentation? StatusPreview(JsonElement arguments)
    {
        if (!TryString(arguments, "item_id", out var itemId) || !TryString(arguments, "status", out var status))
        {
            return null;
        }
        var (validRevision, revision) = Revision(arguments);
        return validRevision ? Presentation($"Set plan item {itemId} to {status}; expected revision {revision}.") : null;
    }

    private static (bool Valid, string Text) Revision(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("expected_revision", out var revision) || revision.ValueKind == JsonValueKind.Null)
        {
            return (true, "none");
        }
        return revision.ValueKind == JsonValueKind.Number && revision.TryGetInt64(out var value)
            ? (true, value.ToString(CultureInfo.InvariantCulture))
            : (false, "");
    }

    private static ToolPresentation? FormatResult(ToolResultPart result, ToolPresentationBounds bounds)
    {
        if (!TryPayload(result, bounds, out var root) || !root.TryGetProperty("plan", out var plan))
        {
            return FailureOnly(result);
        }
        if (plan.ValueKind == JsonValueKind.Null)
        {
            return result.Outcome.Kind == ToolCallOutcomeKind.Success ? Presentation("No current plan.") : FailureOnly(result);
        }
        if (plan.ValueKind != JsonValueKind.Object || !TryString(plan, "id", out var id) || !plan.TryGetProperty("revision", out var revision) ||
            !revision.TryGetInt64(out var revisionValue) || !TryString(plan, "title", out var title) ||
            !plan.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return FailureOnly(result);
        }

        var lines = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (!TryString(item, "id", out var itemId) || !TryString(item, "text", out var text) || !TryString(item, "status", out var status))
            {
                return FailureOnly(result);
            }
            lines.Add($"[{status}] {itemId} — {text}");
        }
        var prefix = result.Outcome.Kind == ToolCallOutcomeKind.Success ? "Plan" : result.Outcome.FailureReason ?? "Plan result failed";
        var summary = $"{prefix}: {title} ({id}), revision {revisionValue}, {lines.Count} item{(lines.Count == 1 ? "" : "s")}.";
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

    private static ToolPresentation Presentation(string text) => new(
        [new ToolPresentationPart(ToolPresentationPartKind.Text, text)], ToolPresentationDisposition.Formatted);
}
