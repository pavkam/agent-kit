// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Describes which kind of JSON root a file binds, so a root written by one store kind is never opened as another.</summary>
/// <param name="StoreKind">The manifest store-kind discriminator the root must carry.</param>
/// <param name="LogName">The stable record-log name inside the root.</param>
/// <param name="HasPayloads">Whether the root also holds a tenant-partitioned payload directory.</param>
internal sealed record JsonArtifactFileKind(string StoreKind, string LogName, bool HasPayloads)
{
    /// <summary>Gets the kind of an artifact store root: an entry log and content-addressed payload files.</summary>
    internal static JsonArtifactFileKind Store { get; } = new("agentkit.artifacts.store", "artifacts", HasPayloads: true);

    /// <summary>Gets the kind of a reference-commit intent store root: one intent log and no payloads.</summary>
    internal static JsonArtifactFileKind Intents { get; } = new("agentkit.artifacts.intents", "intents", HasPayloads: false);
}
