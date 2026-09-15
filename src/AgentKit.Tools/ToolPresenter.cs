// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Text;

/// <summary>Provides bounded exact-descriptor presentation with a generic literal fallback.</summary>
public sealed class ToolPresenter: IToolPresenter
{
    private readonly FrozenDictionary<(ToolSourceId SourceId, ToolId Id, ToolVersion Version), CapturedFormatter> _formatters;

    /// <summary>Captures the complete additive formatter set and rejects duplicate exact source identities.</summary>
    /// <param name="formatters">The nonnull formatter sequence to materialize once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="formatters"/> or an item is null.</exception>
    /// <exception cref="ArgumentException">Two formatters publish the same exact source, identity, and version.</exception>
    public ToolPresenter(IEnumerable<IToolPresentationFormatter> formatters)
    {
        ArgumentNullException.ThrowIfNull(formatters);
        var captured = new Dictionary<(ToolSourceId, ToolId, ToolVersion), CapturedFormatter>();
        foreach (var formatter in formatters)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            var descriptor = formatter.Descriptor;
            ArgumentNullException.ThrowIfNull(descriptor);
            var key = (descriptor.SourceId, descriptor.Id, descriptor.Version);
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(key, new CapturedFormatter(descriptor, formatter)), true, nameof(formatters));
        }
        _formatters = captured.ToFrozenDictionary();
    }

    /// <inheritdoc/>
    public async ValueTask<ToolPresentation> PresentAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        SourceText source;
        try
        {
            source = ExtractSource(request.Source, request.Bounds.MaximumInputBytes);
        }
        catch (InvalidOperationException)
        {
            return Malformed();
        }

        if (source.OmittedCharacters == 0
            && request.Descriptor is { } descriptor
            && _formatters.TryGetValue((descriptor.SourceId, descriptor.Id, descriptor.Version), out var captured)
            && captured.Descriptor == descriptor)
        {
            try
            {
                var formatted = await captured.Formatter.FormatAsync(request, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (formatted is not null)
                {
                    return Bound(formatted.Parts, request.Bounds, formatted.Disposition, formatted.OmittedCharacters);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Presentation is observational. A formatter fault selects the safe generic fallback.
            }
        }

        return Bound(
            [new ToolPresentationPart(ToolPresentationPartKind.Code, source.Text)],
            request.Bounds,
            source.OmittedCharacters > 0 ? ToolPresentationDisposition.Truncated : ToolPresentationDisposition.Fallback,
            source.OmittedCharacters);
    }

    private static SourceText ExtractSource(ToolPresentationSource source, int maximumBytes)
    {
        Debug.Assert(source is not null, "The request validates a source.");
        Debug.Assert(maximumBytes > 0, "Presentation bounds are positive.");
        return source switch
        {
            ToolCallPresentationSource call => LimitUtf8(call.Call.Arguments.GetRawText(), maximumBytes),
            ToolResultPresentationSource result => ExtractResult(result.Result, maximumBytes),
            _ => LimitUtf8("Unsupported tool presentation source omitted.", maximumBytes),
        };
    }

    private static SourceText ExtractResult(ToolResultPart result, int maximumBytes)
    {
        var builder = new StringBuilder();
        var remaining = maximumBytes;
        long omitted = 0;
        AppendBounded(builder, "status: ", ref remaining, ref omitted);
        AppendBounded(builder, result.Outcome.SourceStatus.ToString(), ref remaining, ref omitted);
        AppendBounded(builder, "\n", ref remaining, ref omitted);
        if (result.Outcome.FailureReason is { } failure)
        {
            AppendBounded(builder, "failure: ", ref remaining, ref omitted);
            AppendBounded(builder, failure, ref remaining, ref omitted);
            AppendBounded(builder, "\n", ref remaining, ref omitted);
        }
        for (var index = 0; index < result.Content.Length; index++)
        {
            if (remaining == 0)
            {
                omitted += result.Content.Length - index;
                break;
            }
            var part = result.Content[index];
            var text = part switch
            {
                TextPart value => value.Text,
                StructuredDataPart => "[structured content omitted from generic presentation]",
                MediaReferencePart => "[media content omitted from generic presentation]",
                _ => "[unsupported content omitted from generic presentation]",
            };
            AppendBounded(builder, text, ref remaining, ref omitted);
            AppendBounded(builder, "\n", ref remaining, ref omitted);
        }
        return new SourceText(builder.ToString(), omitted);
    }

    private static SourceText LimitUtf8(string value, int maximumBytes)
    {
        var builder = new StringBuilder(Math.Min(value.Length, maximumBytes));
        var remaining = maximumBytes;
        long omitted = 0;
        AppendBounded(builder, value, ref remaining, ref omitted);
        return new SourceText(builder.ToString(), omitted);
    }

    private static void AppendBounded(StringBuilder builder, string value, ref int remainingBytes, ref long omittedCharacters)
    {
        if (remainingBytes == 0)
        {
            omittedCharacters += value.Length;
            return;
        }
        var consumedCharacters = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var bytes = rune.Utf8SequenceLength;
            if (bytes > remainingBytes)
            {
                break;
            }
            _ = builder.Append(rune);
            remainingBytes -= bytes;
            consumedCharacters += rune.Utf16SequenceLength;
        }
        omittedCharacters += value.Length - consumedCharacters;
    }

    private static ToolPresentation Bound(
        ImmutableArray<ToolPresentationPart> parts,
        ToolPresentationBounds bounds,
        ToolPresentationDisposition disposition,
        long previouslyOmittedCharacters = 0)
    {
        Debug.Assert(!parts.IsDefault, "Formatter output construction requires an initialized part array.");
        var output = ImmutableArray.CreateBuilder<ToolPresentationPart>(Math.Min(parts.Length, bounds.MaximumParts));
        var remaining = bounds.MaximumOutputCharacters;
        var omitted = previouslyOmittedCharacters;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (index >= bounds.MaximumParts || remaining == 0)
            {
                omitted += parts.Length - index;
                break;
            }
            var text = TakeValidUnicode(part.Text, ref remaining, ref omitted);
            var language = TakeOptionalMetadata(part.Language, ref remaining, ref omitted);
            var path = TakeOptionalMetadata(part.Path, ref remaining, ref omitted);
            output.Add(new ToolPresentationPart(part.Kind, text, language, path));
        }
        return new ToolPresentation(output.ToImmutable(), omitted > 0 ? ToolPresentationDisposition.Truncated : disposition, omitted);
    }

    private static string TakeValidUnicode(string value, ref int remaining, ref long omitted)
    {
        var builder = new StringBuilder(Math.Min(value.Length, remaining));
        var consumed = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Utf16SequenceLength > remaining)
            {
                break;
            }
            _ = builder.Append(rune);
            remaining -= rune.Utf16SequenceLength;
            consumed += rune.Utf16SequenceLength;
        }
        omitted += value.Length - consumed;
        return builder.ToString();
    }

    private static string? TakeOptionalMetadata(string? value, ref int remaining, ref long omitted)
    {
        if (value is null)
        {
            return null;
        }
        var retained = TakeValidUnicode(value, ref remaining, ref omitted);
        return retained.Length == 0 ? null : retained;
    }

    private static ToolPresentation Malformed() => new(
        [new ToolPresentationPart(ToolPresentationPartKind.Text, "Tool presentation source is malformed.")],
        ToolPresentationDisposition.Fallback);

    private readonly record struct CapturedFormatter(ToolDescriptor Descriptor, IToolPresentationFormatter Formatter);
    private readonly record struct SourceText(string Text, long OmittedCharacters);
}
