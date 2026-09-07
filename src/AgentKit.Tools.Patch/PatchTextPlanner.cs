// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Resolves parsed line hunks into exact final UTF-8 bytes without host effects.</summary>
internal static class PatchTextPlanner
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>Builds exact LF-encoded bytes for a new file.</summary>
    /// <param name="lines">The parsed addition lines.</param>
    /// <returns>The exact final bytes.</returns>
    internal static ImmutableArray<byte> BuildAddedContent(ImmutableArray<ParsedPatchLine> lines)
    {
        var text = BuildBlock(lines, static _ => true);
        return [.. _strictUtf8.GetBytes(text)];
    }

    /// <summary>Applies exact hunks to strict UTF-8 input while preserving BOM and uniform newline spelling.</summary>
    /// <param name="content">The complete source bytes.</param>
    /// <param name="hunks">The source-ordered parsed hunks.</param>
    /// <param name="finalContent">The exact resulting bytes when successful.</param>
    /// <param name="error">A safe planning explanation when unsuccessful.</param>
    /// <returns><see langword="true"/> only when every hunk has one exact ordered match.</returns>
    internal static bool TryApply(
        ImmutableArray<byte> content,
        ImmutableArray<ParsedPatchHunk> hunks,
        out ImmutableArray<byte> finalContent,
        out string? error)
    {
        ArgumentException.ThrowIfDefault(content);
        ArgumentException.ThrowIfDefaultOrEmpty(hunks);
        finalContent = default;
        error = null;
        var hasBom = content.Length >= 3
            && content[0] == 0xef
            && content[1] == 0xbb
            && content[2] == 0xbf;
        string source;
        try
        {
            source = _strictUtf8.GetString(content.AsSpan()[(hasBom ? 3 : 0)..]);
        }
        catch (DecoderFallbackException)
        {
            error = "A patch update target must be strict UTF-8 text.";
            return false;
        }

        if (source.Contains('\0', StringComparison.Ordinal))
        {
            error = "A patch update target cannot contain binary NUL bytes.";
            return false;
        }

        if (!TryDetectNewline(source, out var newline, out error))
        {
            return false;
        }

        var normalized = newline == "\r\n"
            ? source.Replace("\r\n", "\n", StringComparison.Ordinal)
            : source;
        var searchOffset = 0;
        foreach (var hunk in hunks)
        {
            var oldBlock = BuildBlock(hunk.Lines, static line => line.Prefix is ' ' or '-');
            var newBlock = BuildBlock(hunk.Lines, static line => line.Prefix is ' ' or '+');
            var match = normalized.IndexOf(oldBlock, searchOffset, StringComparison.Ordinal);
            if (match < 0)
            {
                error = "A patch hunk did not match its exact source context.";
                return false;
            }

            if (normalized.IndexOf(oldBlock, match + 1, StringComparison.Ordinal) >= 0)
            {
                error = "A patch hunk source context is ambiguous.";
                return false;
            }

            normalized = string.Concat(
                normalized.AsSpan(0, match),
                newBlock,
                normalized.AsSpan(match + oldBlock.Length));
            searchOffset = match + newBlock.Length;
        }

        var finalText = newline == "\r\n"
            ? normalized.Replace("\n", "\r\n", StringComparison.Ordinal)
            : normalized;
        byte[] body;
        try
        {
            body = _strictUtf8.GetBytes(finalText);
        }
        catch (EncoderFallbackException)
        {
            error = "A patch hunk contains invalid Unicode scalar data.";
            return false;
        }

        finalContent = hasBom ? [0xef, 0xbb, 0xbf, .. body] : [.. body];
        return true;
    }

    private static string BuildBlock(
        ImmutableArray<ParsedPatchLine> lines,
        Func<ParsedPatchLine, bool> include)
    {
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (!include(line))
            {
                continue;
            }

            _ = builder.Append(line.Text);
            if (line.HasTerminator)
            {
                _ = builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static bool TryDetectNewline(string source, out string newline, out string? error)
    {
        var hasLf = false;
        var hasCrlf = false;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\r')
            {
                if (index + 1 >= source.Length || source[index + 1] != '\n')
                {
                    newline = "";
                    error = "A patch update target contains unsupported bare carriage returns.";
                    return false;
                }

                hasCrlf = true;
                index++;
            }
            else if (source[index] == '\n')
            {
                hasLf = true;
            }
        }

        if (hasLf && hasCrlf)
        {
            newline = "";
            error = "A patch update target contains mixed newline spelling.";
            return false;
        }

        newline = hasCrlf ? "\r\n" : "\n";
        error = null;
        return true;
    }
}
