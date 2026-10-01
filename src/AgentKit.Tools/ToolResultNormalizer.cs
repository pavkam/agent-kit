// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Text;

using Microsoft.Extensions.Options;

/// <summary>Maps invoker evidence into bounded normalized terminal content under captured snapshot bounds.</summary>
/// <remarks>
/// Content within the bound is retained as is. Content over the bound is externalized when the executor supplied an
/// <see cref="IToolResultSpill"/>, the all-text content can be stored whole, and the snapshot permits
/// <see cref="ToolResultProjectionTransformations.Externalization"/>: the terminal content then holds a bounded preview and a
/// <see cref="ToolResultArtifactContent"/> naming the committed artifact. Every other oversized result, including one whose
/// spill was refused, is truncated to the bound and recorded as truncated.
/// </remarks>
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
    public async ValueTask<ToolResultNormalizationResult> NormalizeAsync(
        ValidatedToolCall validatedCall,
        ToolInvocationResult invocation,
        ToolResultNormalizationSnapshot snapshot,
        IToolResultSpill? spill = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validatedCall);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        if (invocation.Outcome.Kind is not ToolCallOutcomeKind.Success)
        {
            return new ToolResultNormalized([], new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty));
        }

        var content = MapContent(invocation.Content);
        if (content.Length > snapshot.Bounds.MaximumParts)
        {
            return new ToolResultNormalizationFailed(
                ToolTerminalStatus.ResultNormalizationFailed,
                "The tool result exceeded the configured maximum part count.");
        }

        var inputCanonicalBytes = MeasureCanonicalBytes(content);
        var inputParts = content.Length;
        var maxCanonicalBytes = Math.Min(snapshot.Bounds.MaximumCanonicalBytes, _options.MaximumResultBytes);
        var transformations = ImmutableArray<ToolResultNormalizationTransformation>.Empty;
        long? omittedCanonicalBytes = null;
        if (inputCanonicalBytes > maxCanonicalBytes)
        {
            if (spill is not null
                && snapshot.AllowedTransformations.HasFlag(ToolResultProjectionTransformations.Externalization)
                && TryConcatenateText(content, out var complete)
                && await spill.SpillAsync(new ToolResultSpillRequest(validatedCall, complete), cancellationToken).ConfigureAwait(false)
                    is ToolResultSpilled spilled)
            {
                return Externalize(content, spilled.Reference, maxCanonicalBytes, inputCanonicalBytes, inputParts);
            }

            content = TruncateToCanonicalByteBudget(content, maxCanonicalBytes, out var omitted);
            omittedCanonicalBytes = omitted;
            transformations = [ToolResultNormalizationTransformation.Truncated];
        }

        var canonicalBytes = MeasureCanonicalBytes(content);
        if (canonicalBytes > maxCanonicalBytes)
        {
            return new ToolResultNormalizationFailed(
                ToolTerminalStatus.ResultNormalizationFailed,
                "The tool result exceeded the configured maximum canonical byte bound.");
        }

        var info = new ToolResultNormalizationInfo(
            transformations,
            inputCanonicalBytes,
            inputParts,
            omittedCanonicalBytes,
            omittedCanonicalBytes.HasValue ? 0 : null,
            ExtensionData.Empty);
        return new ToolResultNormalized(content, info);
    }

    private static bool TryConcatenateText(ImmutableArray<ToolResultContent> content, out ImmutableArray<byte> complete)
    {
        var builder = ImmutableArray.CreateBuilder<byte>();
        foreach (var item in content)
        {
            if (item is not ToolResultTextContent text)
            {
                complete = default;
                return false;
            }

            builder.AddRange(Encoding.UTF8.GetBytes(text.Text));
        }

        complete = builder.ToImmutable();
        return complete.Length > 0;
    }

    private ToolResultNormalized Externalize(
        ImmutableArray<ToolResultContent> content,
        ArtifactReference reference,
        long maxCanonicalBytes,
        long inputCanonicalBytes,
        int inputParts)
    {
        var previewBudget = Math.Min(_options.ResultSpillPreviewBytes, maxCanonicalBytes);
        var preview = Utf8Prefix(content, previewBudget);
        var retained = ImmutableArray.CreateBuilder<ToolResultContent>(2);
        long previewBytes = 0;
        if (preview.Length > 0)
        {
            previewBytes = Encoding.UTF8.GetByteCount(preview);
            var first = (ToolResultTextContent) content[0];
            retained.Add(new ToolResultTextContent(preview, first.Semantics, first.Extensions));
        }

        retained.Add(new ToolResultArtifactContent(reference, ExtensionData.Empty));
        var info = new ToolResultNormalizationInfo(
            [ToolResultNormalizationTransformation.Truncated, ToolResultNormalizationTransformation.Externalized],
            inputCanonicalBytes,
            inputParts,
            inputCanonicalBytes - previewBytes,
            0,
            ExtensionData.Empty);
        return new ToolResultNormalized(retained.ToImmutable(), info);
    }

    /// <summary>Returns the longest whole-code-point prefix of the concatenated text that fits the byte budget.</summary>
    private static string Utf8Prefix(ImmutableArray<ToolResultContent> content, long maxBytes)
    {
        var builder = new StringBuilder();
        var used = 0L;
        foreach (var item in content)
        {
            foreach (var rune in ((ToolResultTextContent) item).Text.EnumerateRunes())
            {
                var width = rune.Utf8SequenceLength;
                if (used + width > maxBytes)
                {
                    return builder.ToString();
                }

                _ = builder.Append(rune.ToString());
                used += width;
            }
        }

        return builder.ToString();
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
