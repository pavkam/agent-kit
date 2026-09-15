// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command;

/// <summary>Formats command previews and projected stdout, stderr, and exit evidence as literal typed parts.</summary>
public sealed class CommandToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => CommandTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Source is ToolCallPresentationSource call)
        {
            var arguments = call.Call.Arguments;
            if (arguments.ValueKind != JsonValueKind.Object
                || !arguments.TryGetProperty("command", out var command)
                || command.ValueKind != JsonValueKind.String)
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            if (arguments.TryGetProperty("working_directory", out var directory)
                && directory.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            var location = directory.ValueKind == JsonValueKind.String ? directory.GetString() : "configured default";
            return ValueTask.FromResult<ToolPresentation?>(Presentation([
                new ToolPresentationPart(ToolPresentationPartKind.Text, $"Working directory: {location}"),
                new ToolPresentationPart(ToolPresentationPartKind.Code, command.GetString()!, "shell"),
            ]));
        }
        if (request.Source is not ToolResultPresentationSource result)
        {
            return ValueTask.FromResult<ToolPresentation?>(null);
        }
        var outcome = result.Result.Outcome;
        var summary = outcome.FailureReason is { } reason
            ? $"Outcome: {outcome.SourceStatus}\n{reason}"
            : $"Outcome: {outcome.SourceStatus}";
        if (result.Result.Content.IsEmpty)
        {
            return ValueTask.FromResult<ToolPresentation?>(Presentation([
                new ToolPresentationPart(ToolPresentationPartKind.Text, summary),
            ]));
        }
        if (result.Result.Content is not [TextPart text])
        {
            return ValueTask.FromResult<ToolPresentation?>(null);
        }
        try
        {
            using var document = JsonDocument.Parse(text.Text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            var parts = ImmutableArray.CreateBuilder<ToolPresentationPart>();
            var exit = root.TryGetProperty("exit_code", out var exitCode) && exitCode.ValueKind == JsonValueKind.Number
                ? exitCode.GetRawText() : "unavailable";
            parts.Add(new ToolPresentationPart(ToolPresentationPartKind.Text, $"{summary}\nExit code: {exit}"));
            AddStream(parts, root, "stdout", "stdout_base64", "stdout_truncated");
            AddStream(parts, root, "stderr", "stderr_base64", "stderr_truncated");
            return ValueTask.FromResult<ToolPresentation?>(Presentation(parts.ToImmutable()));
        }
        catch (JsonException)
        {
            return ValueTask.FromResult<ToolPresentation?>(null);
        }
    }

    /// <summary>Adds one independently labeled stream without decoding binary evidence or hiding tail truncation.</summary>
    /// <param name="parts">The validated result builder.</param>
    /// <param name="root">The parsed result object.</param>
    /// <param name="textName">The stream name and decoded text property.</param>
    /// <param name="base64Name">The binary evidence property.</param>
    /// <param name="truncatedName">The upstream truncation flag.</param>
    private static void AddStream(ImmutableArray<ToolPresentationPart>.Builder parts, JsonElement root, string textName, string base64Name, string truncatedName)
    {
        System.Diagnostics.Debug.Assert(parts is not null && root.ValueKind == JsonValueKind.Object, "Result parsing creates a builder and validates the object.");
        var label = root.TryGetProperty(truncatedName, out var truncated) && truncated.ValueKind == JsonValueKind.True
            ? $"{textName} (tail; earlier bytes omitted)" : textName;
        if (root.TryGetProperty(textName, out var text) && text.ValueKind == JsonValueKind.String)
        {
            var value = text.GetString()!;
            parts.Add(new ToolPresentationPart(ToolPresentationPartKind.Text, value.Length == 0 ? $"{label}: empty" : label));
            if (value.Length > 0)
            {
                parts.Add(new ToolPresentationPart(ToolPresentationPartKind.Code, value));
            }
        }
        else if (root.TryGetProperty(base64Name, out var base64) && base64.ValueKind == JsonValueKind.String)
        {
            parts.Add(new ToolPresentationPart(ToolPresentationPartKind.Text, $"{label} (base64; binary output)"));
            parts.Add(new ToolPresentationPart(ToolPresentationPartKind.Code, base64.GetString()!));
        }
        else
        {
            parts.Add(new ToolPresentationPart(ToolPresentationPartKind.Text, $"{label}: unavailable"));
        }
    }

    /// <summary>Builds formatted evidence after successful feature-specific parsing.</summary>
    /// <param name="parts">The initialized literal parts.</param>
    /// <returns>A formatted presentation whose aggregate bounds are enforced by the presenter.</returns>
    private static ToolPresentation Presentation(ImmutableArray<ToolPresentationPart> parts) => new(parts, ToolPresentationDisposition.Formatted);
}
