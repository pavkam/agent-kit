// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Closes the outcomes of selecting the backend that stores a directory's artifacts.</summary>
internal abstract record ArtifactStoreSelectionResult
{
    /// <summary>Prevents other types from extending the closed outcome family.</summary>
    private protected ArtifactStoreSelectionResult()
    {
    }
}
