// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how an embedding vector was normalized by the provider.</summary>
public enum EmbeddingNormalization
{
    /// <summary>The provider did not report a normalization policy.</summary>
    Unspecified,

    /// <summary>The vector was normalized to unit length.</summary>
    Unit,

    /// <summary>The vector was not normalized.</summary>
    None,
}
