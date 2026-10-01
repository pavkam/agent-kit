// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json.Tests;

/// <summary>Owns one temporary store root for a test case and removes it afterwards.</summary>
public sealed class JsonGoalStoreTestRoot: IDisposable
{
    /// <summary>Creates one unique canonical directory a store may treat as an existing root.</summary>
    /// <remarks>The macOS temporary root resolves through a symbolic link, which the store rejects, so the prefix is canonicalized to keep cases exercising the real validation.</remarks>
    public JsonGoalStoreTestRoot()
    {
        var root = System.IO.Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        Path = System.IO.Path.Combine(root, "agentkit-goals-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the root directory path.</summary>
    public string Path { get; }

    /// <summary>Gets a target that creates the root when missing and recovers torn appends.</summary>
    public JsonGoalStoreTarget Target(JsonGoalStoreInstanceId? id = null, JsonStoreOpenMode mode = JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode recovery = JsonStoreRecoveryMode.RecoverTornAppends) =>
        new(Path, id ?? _id, mode, recovery);

    private static readonly JsonGoalStoreInstanceId _id = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    /// <summary>Gets the manifest document path.</summary>
    public string ManifestPath => System.IO.Path.Combine(Path, "store.json");

    /// <summary>Gets the store's log file path.</summary>
    public string LogPath => System.IO.Path.Combine(Path, "goals.jsonl");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
