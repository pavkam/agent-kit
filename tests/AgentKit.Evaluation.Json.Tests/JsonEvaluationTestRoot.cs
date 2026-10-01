// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

/// <summary>Owns one temporary store root for a test case and removes it afterwards.</summary>
public sealed class JsonEvaluationTestRoot: IDisposable
{
    private static readonly JsonEvaluationStoreInstanceId _id = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    /// <summary>Creates one unique canonical directory a store may treat as an existing root.</summary>
    /// <remarks>The macOS temporary root resolves through a symbolic link, which the store rejects, so the prefix is canonicalized to keep cases exercising the real validation.</remarks>
    public JsonEvaluationTestRoot()
    {
        var root = System.IO.Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        Path = System.IO.Path.Combine(root, "agentkit-evaluation-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the root directory path.</summary>
    public string Path { get; }

    /// <summary>Gets the manifest document path.</summary>
    public string ManifestPath => System.IO.Path.Combine(Path, "store.json");

    /// <summary>Gets the result log path.</summary>
    public string LogPath => System.IO.Path.Combine(Path, "results.jsonl");

    /// <summary>Gets a target that creates the root when missing and recovers torn appends.</summary>
    public JsonEvaluationStoreTarget Target(JsonEvaluationStoreInstanceId? id = null, JsonStoreOpenMode mode = JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode recovery = JsonStoreRecoveryMode.RecoverTornAppends) =>
        new(Path, id ?? _id, mode, recovery);

    /// <summary>Opens a store over the root.</summary>
    public JsonEvaluationResultStore Open(JsonEvaluationStoreTarget? target = null, JsonEvaluationStoreSettings? settings = null) =>
        new(target ?? Target(), settings ?? JsonEvaluationStoreSettings.CreateDefault(), TimeProvider.System);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
