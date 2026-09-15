// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write;

/// <summary>Formats write call and projected result evidence as bounded provider-neutral literal parts.</summary>
public sealed class WriteFileToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => WriteFileTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Source is ToolCallPresentationSource call)
        {
            var arguments = call.Call.Arguments;
            if (arguments.ValueKind != JsonValueKind.Object
                || !arguments.TryGetProperty("path", out var path)
                || path.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(path.GetString())
                || !arguments.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String)
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }

            if (!arguments.TryGetProperty("mode", out var modeProperty)
                || modeProperty.ValueKind != JsonValueKind.String)
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            var mode = modeProperty.GetString();
            var effect = mode switch
            {
                "overwrite" => "Create or replace the file",
                "create_or_replace" => "Create or replace the file",
                "create_new" => "Create a new file; fail if it exists",
                "create_only" => "Create a new file; fail if it exists",
                "replace_existing" => "Replace the existing file; fail if it is missing",
                "append" => "Append to the file",
                _ => null,
            };
            return ValueTask.FromResult(effect is null ? null : new ToolPresentation(
                [
                    new ToolPresentationPart(ToolPresentationPartKind.Text, $"Write: {path.GetString()}\n{effect}"),
                    new ToolPresentationPart(ToolPresentationPartKind.Code, content.GetString()!),
                ],
                ToolPresentationDisposition.Formatted));
        }
        if (request.Source is ToolResultPresentationSource result)
        {
            var parts = result.Result.Content.OfType<TextPart>()
                .Select(static part => new ToolPresentationPart(
                    part.Semantics == TextSemantics.Plain ? ToolPresentationPartKind.Text : ToolPresentationPartKind.Code,
                    part.Text))
                .ToImmutableArray();
            if (parts.IsEmpty)
            {
                parts = [new ToolPresentationPart(ToolPresentationPartKind.Text,
                    result.Result.Outcome.FailureReason ?? result.Result.Outcome.SourceStatus.ToString())];
            }
            return ValueTask.FromResult<ToolPresentation?>(new ToolPresentation(parts, ToolPresentationDisposition.Formatted));
        }
        return ValueTask.FromResult<ToolPresentation?>(null);
    }
}
