// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why a retrieval could not produce a result.</summary>
public enum RetrievalFailureKind
{
    /// <summary>The security authority denied the query, or no authority decision could be obtained.</summary>
    Denied = 0,

    /// <summary>The memory profile, its runtime, or a required capability is unavailable.</summary>
    ProfileUnavailable = 1,

    /// <summary>The profile does not enable retrieval.</summary>
    RetrievalDisabled = 2,

    /// <summary>The query's classification ceiling exceeds what the profile permits.</summary>
    ClassificationExceeded = 3,

    /// <summary>The query could not be rewritten and the profile requires rewriting.</summary>
    RewriteFailed = 4,

    /// <summary>No configured source could be selected or every selected source failed.</summary>
    SourcesUnavailable = 5,

    /// <summary>A required embedding operation was unavailable or incompatible with the source's vector space.</summary>
    EmbeddingUnavailable = 6,

    /// <summary>Exposure authorization was required but could not be evaluated.</summary>
    ExposureUnavailable = 7,
}
