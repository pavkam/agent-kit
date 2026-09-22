// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcomes for <see cref="IFileSystemSelector.SelectAsync"/>.</summary>
/// <remarks>A successful selection identifies a capability instance but grants no host access by itself.</remarks>
public abstract record FileSystemSelectionResult
{
    /// <summary>Initializes one closed selection outcome.</summary>
    private protected FileSystemSelectionResult()
    {
    }
}
