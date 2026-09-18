// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web;

/// <summary>Decodes bounded textual web content and removes executable HTML regions deterministically.</summary>
internal static class WebContentProjector
{
    /// <summary>Decodes one already bounded response body.</summary>
    /// <param name="bytes">The complete bounded body bytes.</param>
    /// <param name="contentType">The declared content type, when supplied.</param>
    /// <param name="maximumCharacters">The positive retained character boundary.</param>
    /// <returns>The projected text and loss-aware media diagnostics.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumCharacters"/> is not positive.</exception>
    /// <exception cref="InvalidDataException">The media type, encoding, or byte sequence is unsupported.</exception>
    internal static WebContentProjection Project(
        ReadOnlySpan<byte> bytes,
        string? contentType,
        int maximumCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);
        var declared = ParseContentType(contentType, out var charset);
        var sniffed = Sniff(bytes);
        var mediaType = declared ?? sniffed ?? "text/plain";
        if (!IsSupported(mediaType))
        {
            throw new InvalidDataException($"Media type '{mediaType}' is not supported by web fetch.");
        }

        var encoding = SelectEncoding(bytes, charset);

        // A NUL byte is expected for common characters under a two-byte-per-unit UTF-16 encoding
        // (for example, the low byte of every Latin-1-range code unit) and is not evidence of
        // binary content there; only single-byte-oriented encodings apply this heuristic.
        if (encoding is not UnicodeEncoding && bytes.Contains((byte) 0))
        {
            throw new InvalidDataException("The response appears to contain binary content.");
        }

        string decoded;
        try
        {
            decoded = encoding.GetString(TrimBom(bytes, encoding));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The response is not valid in its declared character encoding.", exception);
        }

        var transform = IsHtml(mediaType) ? "html_to_text_v1" : "text_v1";
        var text = IsHtml(mediaType) ? HtmlToText(decoded) : decoded;
        var truncated = text.Length > maximumCharacters;
        return new WebContentProjection(
            truncated ? text[..TruncationLength(text, maximumCharacters)] : text,
            mediaType,
            declared,
            sniffed,
            encoding.WebName,
            transform,
            truncated);
    }

    /// <summary>
    /// Backs off one UTF-16 code unit from <paramref name="maximumCharacters"/> when the cut would otherwise
    /// land between a high and low surrogate.
    /// </summary>
    /// <param name="text">The text a caller intends to slice at <paramref name="maximumCharacters"/> code units.</param>
    /// <param name="maximumCharacters">The requested, positive UTF-16 code-unit boundary; less than <paramref name="text"/>'s length.</param>
    /// <returns>
    /// <paramref name="maximumCharacters"/> unchanged, or one less when slicing there would split a surrogate
    /// pair and emit a lone surrogate that a strict UTF-8 consumer or <see cref="System.Text.Json.JsonSerializer"/>
    /// would then render as replacement or escaped garbage.
    /// </returns>
    private static int TruncationLength(string text, int maximumCharacters) =>
        maximumCharacters > 0 && char.IsHighSurrogate(text[maximumCharacters - 1])
            ? maximumCharacters - 1
            : maximumCharacters;

    private static string? ParseContentType(string? value, out string? charset)
    {
        charset = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!MediaTypeHeaderValue.TryParse(value, out var parsed) || string.IsNullOrWhiteSpace(parsed.MediaType))
        {
            throw new InvalidDataException("The response Content-Type header is invalid.");
        }

        charset = parsed.CharSet?.Trim('"');
        return parsed.MediaType.ToLowerInvariant();
    }

    private static string? Sniff(ReadOnlySpan<byte> bytes)
    {
        var prefix = Encoding.ASCII.GetString(bytes[..Math.Min(bytes.Length, 256)]).TrimStart();
        return prefix.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                ? "text/html"
                : prefix.StartsWith('{') || prefix.StartsWith('[') ? "application/json" : null;
    }

    private static bool IsSupported(string mediaType) =>
        mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || mediaType is "application/json" or "application/xml" or "application/xhtml+xml"
            or "application/javascript";

    private static bool IsHtml(string mediaType) =>
        mediaType is "text/html" or "application/xhtml+xml";

    private static Encoding SelectEncoding(ReadOnlySpan<byte> bytes, string? charset)
    {
        return bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })
            ? new UTF8Encoding(false, true)
            : bytes.StartsWith(new byte[] { 0xff, 0xfe })
            ? new UnicodeEncoding(false, true, true)
            : bytes.StartsWith(new byte[] { 0xfe, 0xff })
            ? new UnicodeEncoding(true, true, true)
            : charset?.ToLowerInvariant() switch
            {
                null or "utf-8" or "utf8" => new UTF8Encoding(false, true),
                "utf-16" or "utf-16le" or "unicode" => new UnicodeEncoding(false, false, true),
                "utf-16be" => new UnicodeEncoding(true, false, true),
                "iso-8859-1" or "latin1" => Encoding.Latin1,
                "us-ascii" or "ascii" => Encoding.GetEncoding(
                    "us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
                _ => throw new InvalidDataException($"Character encoding '{charset}' is not supported by web fetch."),
            };
    }

    private static ReadOnlySpan<byte> TrimBom(ReadOnlySpan<byte> bytes, Encoding encoding) =>
        encoding.CodePage switch
        {
            65001 when bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) => bytes[3..],
            1200 when bytes.StartsWith(new byte[] { 0xff, 0xfe }) => bytes[2..],
            1201 when bytes.StartsWith(new byte[] { 0xfe, 0xff }) => bytes[2..],
            _ => bytes,
        };

    private static string HtmlToText(string html)
    {
        var builder = new StringBuilder(html.Length);
        for (var index = 0; index < html.Length;)
        {
            if (html[index] != '<')
            {
                _ = builder.Append(html[index++]);
                continue;
            }

            var close = html.IndexOf('>', index + 1);
            if (close < 0)
            {
                _ = builder.Append(html.AsSpan(index));
                break;
            }

            var tag = TagName(html.AsSpan(index + 1, close - index - 1));
            if (tag is "script" or "style" or "object" or "embed")
            {
                var closing = $"</{tag}";
                var closingStart = html.IndexOf(closing, close + 1, StringComparison.OrdinalIgnoreCase);
                if (closingStart < 0)
                {
                    break;
                }

                var closingEnd = html.IndexOf('>', closingStart + closing.Length);
                index = closingEnd < 0 ? html.Length : closingEnd + 1;
                continue;
            }

            if (tag is "br" or "p" or "/p" or "div" or "/div" or "li" or "/li"
                or "h1" or "/h1" or "h2" or "/h2" or "h3" or "/h3" or "tr" or "/tr")
            {
                _ = builder.Append('\n');
            }

            index = close + 1;
        }

        return NormalizeWhitespace(WebUtility.HtmlDecode(builder.ToString()));
    }

    private static string TagName(ReadOnlySpan<char> raw)
    {
        raw = raw.TrimStart();
        var length = 0;
        if (!raw.IsEmpty && raw[0] == '/')
        {
            length++;
        }

        while (length < raw.Length && (char.IsAsciiLetterOrDigit(raw[length]) || raw[length] is '-' or '/'))
        {
            length++;
        }

        return raw[..length].ToString().ToLowerInvariant();
    }

    private static string NormalizeWhitespace(string value)
    {
        var output = new StringBuilder(value.Length);
        var whitespace = false;
        var newlines = 0;
        foreach (var character in value)
        {
            if (character is '\r' or '\n')
            {
                while (output.Length > 0 && output[^1] == ' ')
                {
                    output.Length--;
                }

                newlines++;
                whitespace = false;
                if (newlines <= 2 && output.Length > 0)
                {
                    _ = output.Append('\n');
                }

                continue;
            }

            newlines = 0;
            if (char.IsWhiteSpace(character))
            {
                whitespace = output.Length > 0;
                continue;
            }

            if (whitespace)
            {
                _ = output.Append(' ');
                whitespace = false;
            }

            _ = output.Append(character);
        }

        return output.ToString().Trim();
    }
}

/// <summary>Contains one deterministic, loss-aware textual web projection.</summary>
/// <param name="Text">The retained untrusted remote text.</param>
/// <param name="MediaType">The effective media type used for conversion.</param>
/// <param name="DeclaredMediaType">The declared media type, when present.</param>
/// <param name="SniffedMediaType">The conservatively sniffed media type, when recognized.</param>
/// <param name="Encoding">The effective character encoding.</param>
/// <param name="Transform">The deterministic transform identifier.</param>
/// <param name="Truncated">Whether model projection omitted trailing characters.</param>
internal sealed record WebContentProjection(
    string Text,
    string MediaType,
    string? DeclaredMediaType,
    string? SniffedMediaType,
    string Encoding,
    string Transform,
    bool Truncated);
