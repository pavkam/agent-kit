// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Parses the pinned line-oriented <c>agentkit-patch-v1</c> grammar without host effects.</summary>
internal static class AgentKitPatchParser
{
    /// <summary>Parses one complete patch envelope.</summary>
    /// <param name="text">The patch text.</param>
    /// <param name="maximumEntries">The positive syntax-level entry bound.</param>
    /// <param name="patch">The parsed plan when successful.</param>
    /// <param name="error">A safe syntax explanation when parsing fails.</param>
    /// <returns><see langword="true"/> only for a complete valid envelope.</returns>
    internal static bool TryParse(
        string text,
        int maximumEntries,
        out ParsedPatch? patch,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        patch = null;
        error = null;
        var lines = ReadLines(text);
        if (lines.Count < 3 || lines[0] != "*** Begin Patch" || lines[^1] != "*** End Patch")
        {
            error = "The patch must have exact Begin Patch and End Patch envelope lines.";
            return false;
        }

        var entries = ImmutableArray.CreateBuilder<ParsedPatchEntry>();
        var touched = new HashSet<string>(StringComparer.Ordinal);
        var index = 1;
        while (index < lines.Count - 1)
        {
            if (entries.Count >= maximumEntries)
            {
                error = "The patch exceeds its entry bound.";
                return false;
            }

            var header = lines[index++];
            if (TryHeader(header, "*** Add File: ", out var addPath))
            {
                if (!TryPath(addPath, touched, out var path, out error))
                {
                    return false;
                }

                var content = ImmutableArray.CreateBuilder<ParsedPatchLine>();
                while (index < lines.Count - 1 && !lines[index].StartsWith("*** ", StringComparison.Ordinal))
                {
                    var line = lines[index++];
                    if (line == "\\ No newline at end of file")
                    {
                        if (!RemoveLastTerminator(content, '+'))
                        {
                            error = "A no-newline marker must follow an added line.";
                            return false;
                        }

                        continue;
                    }

                    if (line.Length == 0 || line[0] != '+')
                    {
                        error = "Every add-file content line must start with '+'.";
                        return false;
                    }

                    content.Add(new ParsedPatchLine('+', line[1..], true));
                }

                entries.Add(new ParsedPatchEntry(
                    ParsedPatchEntryKind.Add, path, null, content.ToImmutable(), []));
                continue;
            }

            if (TryHeader(header, "*** Delete File: ", out var deletePath))
            {
                if (!TryPath(deletePath, touched, out var path, out error))
                {
                    return false;
                }

                if (index < lines.Count - 1 && !lines[index].StartsWith("*** ", StringComparison.Ordinal))
                {
                    error = "Delete entries cannot contain a body.";
                    return false;
                }

                entries.Add(new ParsedPatchEntry(ParsedPatchEntryKind.Delete, path, null, [], []));
                continue;
            }

            if (!TryHeader(header, "*** Update File: ", out var updatePath)
                || !TryPath(updatePath, touched, out var source, out error))
            {
                error ??= "Expected an Add File, Update File, or Delete File header.";
                return false;
            }

            FileSystemPath? destination = null;
            if (index < lines.Count - 1 && TryHeader(lines[index], "*** Move to: ", out var destinationText))
            {
                index++;
                if (!TryPath(destinationText, touched, out var destinationValue, out error))
                {
                    return false;
                }

                destination = destinationValue;
            }

            var hunks = ImmutableArray.CreateBuilder<ParsedPatchHunk>();
            while (index < lines.Count - 1 && !IsEntryHeader(lines[index]))
            {
                if (!lines[index++].StartsWith("@@", StringComparison.Ordinal))
                {
                    error = "Every update section must start with an '@@' hunk marker.";
                    return false;
                }

                var hunkLines = ImmutableArray.CreateBuilder<ParsedPatchLine>();
                while (index < lines.Count - 1
                    && !lines[index].StartsWith("@@", StringComparison.Ordinal)
                    && !IsEntryHeader(lines[index]))
                {
                    var line = lines[index++];
                    if (line == "\\ No newline at end of file")
                    {
                        if (!RemoveLastTerminator(hunkLines, null))
                        {
                            error = "A no-newline marker must follow a hunk line.";
                            return false;
                        }

                        continue;
                    }

                    if (line.Length == 0 || line[0] is not (' ' or '-' or '+'))
                    {
                        error = "Hunk lines must start with space, '-', or '+'.";
                        return false;
                    }

                    hunkLines.Add(new ParsedPatchLine(line[0], line[1..], true));
                }

                if (hunkLines.Count == 0 || hunkLines.All(static line => line.Prefix == '+'))
                {
                    error = "An update hunk requires source context or removed lines.";
                    return false;
                }

                hunks.Add(new ParsedPatchHunk(hunkLines.ToImmutable()));
            }

            if (destination is not null && hunks.Count > 0)
            {
                error = "A move entry cannot also change content in agentkit-patch-v1.";
                return false;
            }

            if (destination is null && hunks.Count == 0)
            {
                error = "An update entry requires at least one hunk.";
                return false;
            }

            entries.Add(new ParsedPatchEntry(
                destination is null ? ParsedPatchEntryKind.Update : ParsedPatchEntryKind.Move,
                source,
                destination,
                [],
                hunks.ToImmutable()));
        }

        if (entries.Count == 0)
        {
            error = "The patch must contain at least one entry.";
            return false;
        }

        patch = new ParsedPatch(entries.ToImmutable());
        return true;
    }

    private static List<string> ReadLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static bool TryHeader(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = line[prefix.Length..];
            return true;
        }

        value = "";
        return false;
    }

    private static bool TryPath(
        string text,
        HashSet<string> touched,
        out FileSystemPath path,
        out string? error)
    {
        try
        {
            path = new FileSystemPath(text);
        }
        catch (ArgumentException exception)
        {
            path = default;
            error = exception.Message;
            return false;
        }

        if (!touched.Add(path.Value))
        {
            error = "The patch cannot touch the same path more than once.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsEntryHeader(string line) =>
        line.StartsWith("*** Add File: ", StringComparison.Ordinal)
        || line.StartsWith("*** Update File: ", StringComparison.Ordinal)
        || line.StartsWith("*** Delete File: ", StringComparison.Ordinal)
        || line == "*** End Patch";

    private static bool RemoveLastTerminator(
        ImmutableArray<ParsedPatchLine>.Builder lines,
        char? requiredPrefix)
    {
        if (lines.Count == 0 || (requiredPrefix is not null && lines[^1].Prefix != requiredPrefix))
        {
            return false;
        }

        lines[^1] = lines[^1] with { HasTerminator = false };
        return true;
    }
}
