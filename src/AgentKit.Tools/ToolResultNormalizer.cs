// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Text;

using Microsoft.Extensions.Options;

/// <summary>Maps invoker evidence into bounded normalized terminal content under captured snapshot bounds.</summary>
public sealed class ToolResultNormalizer: IToolResultNormalizer
{
    private readonly ToolRuntimeOptions _options;

    /// <summary>Initializes the first-party result normalizer.</summary>
    /// <param name="options">The configured runtime byte limits applied in addition to each snapshot bound.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public ToolResultNormalizer(IOptions<ToolRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

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

        var inputCanonicalBytes = MeasureCanonicalBytes(content);
        var inputParts = content.Length;
        var maxCanonicalBytes = Math.Min(snapshot.Bounds.MaximumCanonicalBytes, _options.MaximumResultBytes);
        var transformations = ImmutableArray<ToolResultNormalizationTransformation>.Empty;
        long? omittedCanonicalBytes = null;
        if (inputCanonicalBytes > maxCanonicalBytes)
        {
            content = TruncateToCanonicalByteBudget(content, maxCanonicalBytes, out var omitted);
            omittedCanonicalBytes = omitted;
            transformations = [ToolResultNormalizationTransformation.Truncated];
        }

        var canonicalBytes = MeasureCanonicalBytes(content);
        if (canonicalBytes > maxCanonicalBytes)
        {
            return ValueTask.FromResult<ToolResultNormalizationResult>(
                new ToolResultNormalizationFailed(
                    ToolTerminalStatus.ResultNormalizationFailed,
                    "The tool result exceeded the configured maximum canonical byte bound."));
        }

        var info = new ToolResultNormalizationInfo(
            transformations,
            inputCanonicalBytes,
            inputParts,
            omittedCanonicalBytes,
            omittedCanonicalBytes.HasValue ? 0 : null,
            ExtensionData.Empty);
        return ValueTask.FromResult<ToolResultNormalizationResult>(new ToolResultNormalized(content, info));
    }

    private static long MeasureCanonicalBytes(ImmutableArray<ToolResultContent> content)
    {
        long total = 0;
        foreach (var item in content)
        {
            total = checked(total + MeasureItemCanonicalBytes(item));
        }

        return total;
    }

    private static long MeasureItemCanonicalBytes(ToolResultContent item) =>
        item switch
        {
            ToolResultTextContent text => Encoding.UTF8.GetByteCount(text.Text),
            ToolResultOpaqueContent opaque => opaque.CanonicalPayload.CanonicalJson.Length,
            _ => 0,
        };

    private static ImmutableArray<ToolResultContent> TruncateToCanonicalByteBudget(
        ImmutableArray<ToolResultContent> content,
        long maxCanonicalBytes,
        out long omittedBytes)
    {
        omittedBytes = 0;
        var builder = ImmutableArray.CreateBuilder<ToolResultContent>(content.Length);
        var remaining = maxCanonicalBytes;
        foreach (var item in content)
        {
            if (remaining <= 0)
            {
                omittedBytes = checked(omittedBytes + MeasureItemCanonicalBytes(item));
                continue;
            }

            if (item is ToolResultTextContent text)
            {
                var bytes = Encoding.UTF8.GetBytes(text.Text);
                if (bytes.Length <= remaining)
                {
                    builder.Add(text);
                    remaining -= bytes.Length;
                    continue;
                }

                var truncated = Encoding.UTF8.GetString(bytes.AsSpan(0, (int) remaining));
                builder.Add(new ToolResultTextContent(truncated, text.Semantics, text.Extensions));
                omittedBytes = checked(omittedBytes + bytes.Length - remaining);
                remaining = 0;
                continue;
            }

            var opaqueBytes = item switch
            {
                ToolResultOpaqueContent opaque => opaque.CanonicalPayload.CanonicalJson.Length,
                _ => 0L,
            };
            if (opaqueBytes <= remaining)
            {
                builder.Add(item);
                remaining -= opaqueBytes;
            }
            else
            {
                omittedBytes = checked(omittedBytes + opaqueBytes);
            }
        }

        return builder.ToImmutable();
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
