// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Text;

/// <summary>Maps invoker evidence into bounded normalized terminal content under captured snapshot bounds.</summary>
public sealed class ToolResultNormalizer: IToolResultNormalizer
{
    /// <inheritdoc/>
    public ValueTask<ToolResultNormalizationResult> NormalizeAsync(
        ValidatedToolCall validatedCall,
        ToolInvocationResult invocation,
        ToolResultNormalizationSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validatedCall);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        if (invocation.Outcome.Kind is not ToolCallOutcomeKind.Success)
        {
            return ValueTask.FromResult<ToolResultNormalizationResult>(
                new ToolResultNormalized([], new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty)));
        }

        var content = MapContent(invocation.Content);
        if (content.Length > snapshot.Bounds.MaximumParts)
        {
            return ValueTask.FromResult<ToolResultNormalizationResult>(
                new ToolResultNormalizationFailed(
                    ToolTerminalStatus.ResultNormalizationFailed,
                    "The tool result exceeded the configured maximum part count."));
        }

        var canonicalBytes = MeasureCanonicalBytes(content);
        if (canonicalBytes > snapshot.Bounds.MaximumCanonicalBytes)
        {
            return ValueTask.FromResult<ToolResultNormalizationResult>(
                new ToolResultNormalizationFailed(
                    ToolTerminalStatus.ResultNormalizationFailed,
                    "The tool result exceeded the configured maximum canonical byte bound."));
        }

        var info = new ToolResultNormalizationInfo(
            [],
            canonicalBytes,
            content.Length,
            null,
            null,
            ExtensionData.Empty);
        return ValueTask.FromResult<ToolResultNormalizationResult>(new ToolResultNormalized(content, info));
    }

    private static long MeasureCanonicalBytes(ImmutableArray<ToolResultContent> content)
    {
        long total = 0;
        foreach (var item in content)
        {
            total = checked(total + item switch
            {
                ToolResultTextContent text => Encoding.UTF8.GetByteCount(text.Text),
                ToolResultOpaqueContent opaque => opaque.CanonicalPayload.CanonicalJson.Length,
                _ => 0,
            });
        }

        return total;
    }

    private static ImmutableArray<ToolResultContent> MapContent(ImmutableArray<ContentPart> parts)
    {
        if (parts.IsDefault || parts.Length == 0)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<ToolResultContent>(parts.Length);
        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    builder.Add(new ToolResultTextContent(text.Text, text.Semantics, text.Extensions));
                    break;
                case ToolResultPart:
                    continue;
                default:
                    builder.Add(new ToolResultOpaqueContent(
                        "tool.content-part",
                        new ExtensionValue([(byte) '{', (byte) '}']),
                        ExtensionData.Empty));
                    break;
            }
        }

        return builder.ToImmutable();
    }
}
