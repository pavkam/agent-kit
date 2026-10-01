// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Names bytes an adapter must durably stage before the entry that owns them is persisted.</summary>
/// <param name="Entry">The entry that will own the payload.</param>
/// <param name="Content">The exact complete bytes.</param>
internal sealed record ArtifactPayloadStage(ArtifactEntry Entry, ImmutableArray<byte> Content);
