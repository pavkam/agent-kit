// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares whether published artifact content can produce later versions.</summary>
public enum ArtifactMutability
{
    /// <summary>Published bytes never change.</summary>
    Immutable,
    /// <summary>New versions may append while preserving prior versions.</summary>
    AppendOnly,
    /// <summary>Content lifecycle is owned externally.</summary>
    ExternallyManaged,
}
