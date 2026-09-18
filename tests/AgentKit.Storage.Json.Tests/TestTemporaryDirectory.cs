// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Owns one unique canonical temporary directory for file-mechanics cases and removes it on disposal.</summary>
/// <remarks>
/// <para>
/// <see cref="JsonStoreRoot"/> rejects any root reached by traversing a replaceable link, and macOS resolves the system
/// temporary directory through the <c>/var</c> symbolic link. Canonicalizing the prefix keeps every non-link case exercising
/// the behavior actually under test instead of tripping link validation for an unrelated reason.
/// </para>
/// <para>
/// The fixture is deliberately not a store adapter: it only creates and removes a directory, so a case never depends on a
/// shared machine-wide path or on cleanup performed by another case.
/// </para>
/// </remarks>
internal sealed class TestTemporaryDirectory: IDisposable
{
    /// <summary>Creates one unique canonical directory on disk.</summary>
    /// <remarks>The directory exists when the constructor returns, so a case may immediately write into it.</remarks>
    internal TestTemporaryDirectory()
    {
        var root = System.IO.Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        Path = System.IO.Path.Combine(root, $"agentkit-storage-json-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the fully qualified canonical directory this fixture owns.</summary>
    /// <value>An existing directory whose entire ancestry contains no symbolic link or reparse point.</value>
    internal string Path { get; }

    /// <summary>Resolves one path inside this directory without creating anything on disk.</summary>
    /// <param name="name">The single path-free file or directory name to append.</param>
    /// <returns>The fully qualified combined path, which need not exist.</returns>
    internal string Combine(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>Lists the atomic-replacement residue currently left directly inside this directory.</summary>
    /// <returns>Every <c>.tmp</c> file path in this directory, which must be empty after a successful or failed replacement.</returns>
    internal string[] TemporaryFiles() => Directory.GetFiles(Path, "*.tmp");

    /// <summary>Removes the directory and everything a case wrote into it.</summary>
    /// <remarks>
    /// Disposal is safe when the directory was already removed. A case holding a <see cref="JsonStoreLock"/> must release it
    /// first, because the lock file is deleted only when its handle closes.
    /// </remarks>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
