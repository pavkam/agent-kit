// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Names the lifecycle state of one preparation in a tenant partition.</summary>
internal enum ArtifactEntryState
{
    /// <summary>Bytes are staged and unpublished; they are never readable as committed content.</summary>
    Prepared = 0,

    /// <summary>The preparation was atomically published as an immutable committed version.</summary>
    Finalized = 1,

    /// <summary>The preparation was aborted or expired; it can never be published.</summary>
    Aborted = 2,
}
