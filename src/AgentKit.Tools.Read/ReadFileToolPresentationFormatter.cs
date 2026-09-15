// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>Formats read call and projected result evidence as bounded provider-neutral literal parts.</summary>
public sealed class ReadFileToolPresentationFormatter: IToolPresentationFormatter
{
    /// <inheritdoc/>
    public ToolDescriptor Descriptor => ReadFileTool.PresentationDescriptor;

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Source is ToolCallPresentationSource call)
        {
            var arguments = call.Call.Arguments;
            if (arguments.ValueKind != JsonValueKind.Object)
            {
                return ValueTask.FromResult<ToolPresentation?>(null);
            }
            var value = arguments.TryGetProperty("path", out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : arguments.GetRawText();
            return ValueTask.FromResult<ToolPresentation?>(new ToolPresentation(
                [new ToolPresentationPart(ToolPresentationPartKind.Text, "Read: " + value)],
                ToolPresentationDisposition.Formatted));
        }
        if (request.Source is ToolResultPresentationSource result)
        {
            var parts = result.Result.Content.OfType<TextPart>()
                .Select(static part => new ToolPresentationPart(
                    ToolPresentationPartKind.Code,
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
