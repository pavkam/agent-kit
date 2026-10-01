// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Owns one unique temporary directory whose subdirectories serve as independent JSON store roots.</summary>
public sealed class JsonMemoryTestRoot: IDisposable
{
    private static readonly JsonMemoryInstanceId _id = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    /// <summary>Creates one unique canonical directory a store may treat as an existing root.</summary>
    /// <remarks>The macOS temporary root resolves through a symbolic link, which the store rejects, so the prefix is canonicalized to keep cases exercising the real validation.</remarks>
    public JsonMemoryTestRoot()
    {
        var root = System.IO.Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        Path = System.IO.Path.Combine(root, "agentkit-memory-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the root directory path.</summary>
    public string Path { get; }

    /// <summary>Gets the path of one named store root beneath the test directory.</summary>
    /// <param name="name">The store root name.</param>
    /// <returns>The directory path, created when missing.</returns>
    public string StoreDirectory(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Gets a target that creates the root when missing and recovers torn appends.</summary>
    /// <param name="name">The store root name.</param>
    /// <param name="id">The instance identity, or the shared test identity.</param>
    /// <param name="mode">Whether a missing root may be created.</param>
    /// <param name="recovery">Whether a torn trailing append is recovered or refused.</param>
    /// <returns>The target.</returns>
    public JsonMemoryTarget Target(string name = "store", JsonMemoryInstanceId? id = null, JsonStoreOpenMode mode = JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode recovery = JsonStoreRecoveryMode.RecoverTornAppends) =>
        new(StoreDirectory(name), id ?? _id, mode, recovery);

    /// <summary>Gets the manifest path of one store root.</summary>
    /// <param name="name">The store root name.</param>
    /// <returns>The manifest document path.</returns>
    public string ManifestPath(string name = "store") => System.IO.Path.Combine(StoreDirectory(name), "store.json");

    /// <summary>Gets the log path of one store root.</summary>
    /// <param name="name">The store root name.</param>
    /// <param name="log">The log file name without extension.</param>
    /// <returns>The log file path.</returns>
    public string LogPath(string name, string log) => System.IO.Path.Combine(StoreDirectory(name), log + ".jsonl");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
