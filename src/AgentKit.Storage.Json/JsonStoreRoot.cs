// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Resolves and validates the fixed on-disk layout of one JSON store root.</summary>
/// <remarks>
/// Path validation detects accidental or ordinary replacement of the configured root through symbolic links and reparse
/// points. It is a consistency check, not operating-system isolation: trusted bootstrap remains responsible for directory
/// ownership, access control, and confinement. The root authorizes no ordinary file operation on behalf of an agent.
/// </remarks>
public sealed class JsonStoreRoot
{
    private const string _manifestFileName = "store.json";
    private const string _lockFileName = "store.lock";

    /// <summary>Binds the layout to one normalized directory without creating or probing it.</summary>
    /// <param name="directoryPath">The fully qualified store root directory.</param>
    /// <exception cref="ArgumentException"><paramref name="directoryPath"/> is null or blank.</exception>
    public JsonStoreRoot(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        DirectoryPath = Path.GetFullPath(directoryPath);
        ManifestPath = Path.Combine(DirectoryPath, _manifestFileName);
        LockPath = Path.Combine(DirectoryPath, _lockFileName);
    }

    /// <summary>Gets the normalized root directory.</summary>
    /// <value>The fully qualified directory; callers treat it as sensitive bootstrap configuration and never log it.</value>
    public string DirectoryPath { get; }

    /// <summary>Gets the store manifest document path.</summary>
    /// <value>The fully qualified path of the identity, version, and encoding-contract document.</value>
    public string ManifestPath { get; }

    /// <summary>Gets the advisory exclusive lock path.</summary>
    /// <value>The fully qualified path of the file held open for the adapter's lifetime.</value>
    public string LockPath { get; }

    /// <summary>Resolves the fully qualified path of one named record log inside this root.</summary>
    /// <param name="name">The nonblank stable log name without an extension, such as <c>grants</c>.</param>
    /// <returns>The fully qualified newline-delimited record-log path.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, blank, or contains a path separator.</exception>
    public string LogPath(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.AsSpan().ContainsAny(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            || name.Contains("..", StringComparison.Ordinal)
            ? throw new ArgumentException("A record-log name must be a single path-free segment.", nameof(name))
            : Path.Combine(DirectoryPath, $"{name}.jsonl");
    }

    /// <summary>Validates that the configured root is a real directory reached without traversing a replaceable link.</summary>
    /// <param name="allowCreate">Whether a missing root directory may be created by this call.</param>
    /// <exception cref="InvalidOperationException">The root is missing and creation is not permitted, or a path component is a link.</exception>
    /// <exception cref="IOException">The root cannot be inspected or created.</exception>
    /// <remarks>
    /// Creation is a declared bootstrap effect and creates only the exact configured directory and any missing parents the
    /// host already authorized by configuring the path. The check runs before every access so a root replaced after
    /// initialization is detected rather than silently used.
    /// </remarks>
    public void Validate(bool allowCreate)
    {
        var directory = new DirectoryInfo(DirectoryPath);
        if (!directory.Exists)
        {
            if (!allowCreate)
            {
                throw new InvalidOperationException("The configured JSON store root directory does not exist.");
            }

            _ = Directory.CreateDirectory(DirectoryPath);
            directory = new DirectoryInfo(DirectoryPath);
        }

        for (var current = directory; current is not null; current = current.Parent)
        {
            if (current.LinkTarget is not null || current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException("The JSON store root traverses a replaceable link.");
            }
        }
    }

    /// <summary>Validates that one store file is a regular file rather than a replaceable link.</summary>
    /// <param name="path">The fully qualified file path inside this root.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    /// <exception cref="InvalidOperationException">The existing file is a symbolic link or reparse point.</exception>
    public static void ValidateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(path);
        if (file.LinkTarget is not null || (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint)))
        {
            throw new InvalidOperationException("A JSON store file is a replaceable link.");
        }
    }
}
