// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Decides whether a user-entered folder may be exposed read-only to sandboxed commands.</summary>
/// <remarks>
/// The policy is pure: the caller supplies the workspace, the folders already listed, the home
/// directory used for <c>~</c> expansion, and a directory-existence probe, so the same rules are
/// testable without touching the real file system. It never widens access on its own; an accepted
/// path still flows through the ordinary security authority when a command runs.
/// </remarks>
internal static class ToolchainRootPolicy
{
    /// <summary>Normalizes and validates one candidate read-only folder.</summary>
    /// <param name="input">The raw text the user typed; may be blank.</param>
    /// <param name="workspaceRoot">The absolute writable workspace root.</param>
    /// <param name="existingRoots">The absolute folders already listed, compared ordinally.</param>
    /// <param name="homeDirectory">The absolute home directory that a leading <c>~</c> expands to, or null to disable expansion.</param>
    /// <param name="directoryExists">Reports whether one absolute path is an existing directory.</param>
    /// <returns>An accepted outcome with the normalized path, or a rejected outcome with the reason.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is blank or not absolute.</exception>
    public static ToolchainRootValidation Validate(
        string input,
        string workspaceRoot,
        IEnumerable<string> existingRoots,
        string? homeDirectory,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(existingRoots);
        ArgumentNullException.ThrowIfNull(directoryExists);
        if (!Path.IsPathFullyQualified(workspaceRoot))
        {
            throw new ArgumentException("The workspace root must be absolute.", nameof(workspaceRoot));
        }

        var text = input.Trim();
        if (text.Length == 0)
        {
            return ToolchainRootValidation.Rejected("Enter a folder path.");
        }

        if (homeDirectory is { Length: > 0 } && (text == "~" || text.StartsWith("~/", StringComparison.Ordinal)))
        {
            text = Path.Combine(homeDirectory, text.Length == 1 ? "" : text[2..]);
        }

        if (!Path.IsPathFullyQualified(text))
        {
            return ToolchainRootValidation.Rejected("Use an absolute path, such as /opt/homebrew or ~/.dotnet.");
        }

        string candidate;
        try
        {
            candidate = Normalize(Path.GetFullPath(text));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ToolchainRootValidation.Rejected("That is not a valid folder path.");
        }

        var workspace = Normalize(Path.GetFullPath(workspaceRoot));
        var rejection =
            candidate == workspace || IsInside(candidate, workspace)
                ? "The workspace is already writable; pick a folder outside it."
                : IsInside(workspace, candidate)
                    ? "That folder contains the workspace; choose a narrower folder."
                    : existingRoots.Any(root => string.Equals(Normalize(root), candidate, StringComparison.Ordinal))
                        ? "That folder is already listed."
                        : directoryExists(candidate)
                            ? null
                            : "That folder does not exist.";
        return rejection is null
            ? ToolchainRootValidation.Accepted(candidate)
            : ToolchainRootValidation.Rejected(rejection);
    }

    /// <summary>Strips trailing separators so equal folders compare equal, keeping a bare root intact.</summary>
    /// <param name="path">The absolute path to normalize.</param>
    /// <returns>The path without trailing separators, or the unchanged filesystem root.</returns>
    private static string Normalize(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? path[..1] : trimmed;
    }

    /// <summary>Determines whether <paramref name="path"/> sits strictly below <paramref name="ancestor"/>.</summary>
    /// <param name="path">A normalized absolute path.</param>
    /// <param name="ancestor">A normalized absolute path.</param>
    /// <returns><see langword="true"/> when <paramref name="ancestor"/> is a proper prefix at a separator boundary.</returns>
    private static bool IsInside(string path, string ancestor)
    {
        var prefix = ancestor.EndsWith(Path.DirectorySeparatorChar) ? ancestor : ancestor + Path.DirectorySeparatorChar;
        return path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
