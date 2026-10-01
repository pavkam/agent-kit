// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closes the outcomes of aborting one staged preparation in a store.</summary>
public abstract record ArtifactStoreAbortResult
{
    /// <summary>Prevents outside assemblies from extending the closed outcome family.</summary>
    private protected ArtifactStoreAbortResult()
    {
    }
}
