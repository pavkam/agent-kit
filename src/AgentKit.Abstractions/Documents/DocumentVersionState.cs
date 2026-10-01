// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records where one document version stands in its publication.</summary>
public enum DocumentVersionState
{
    /// <summary>The complete chunk set is stored but retrieval does not see it yet.</summary>
    Staged = 0,

    /// <summary>The version is the document's single active version that retrieval and exposure may use.</summary>
    Active = 1,

    /// <summary>A newer version became active; the version remains stored for citation but is never retrieved.</summary>
    Superseded = 2,
}
