// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Engine-wide memory and retrieval ceilings and defaults.</summary>
/// <remarks>These are host ceilings. A memory profile may narrow the retrieval bounds but never widen them, and a per-query budget may only narrow the result further. Defaults enable no store, source, embedding, or reranker.</remarks>
public sealed class AgentMemoryOptions
{
    /// <summary>Gets or sets how policies combine.</summary>
    /// <value>Defaults to <see cref="MemoryAcceptanceMode.RequireExplicitPolicyAllow"/>: proposal policy denies until an explicit policy allows retention.</value>
    public MemoryAcceptanceMode AcceptanceMode { get; set; } = MemoryAcceptanceMode.RequireExplicitPolicyAllow;

    /// <summary>Gets or sets the engine ceiling for candidates one retrieval returns.</summary>
    /// <value>A positive bound. The default is 20.</value>
    public int MaximumRetrievedItems { get; set; } = 20;

    /// <summary>Gets or sets the engine ceiling for the UTF-8 bytes of candidate text one retrieval returns.</summary>
    /// <value>A positive bound. The default is 262,144.</value>
    public int MaximumRetrievedBytes { get; set; } = 262_144;

    /// <summary>Gets or sets the engine ceiling for the estimated tokens of candidate text one retrieval returns.</summary>
    /// <value>A positive bound. The default is 8,192.</value>
    public int MaximumRetrievalTokens { get; set; } = 8_192;

    /// <summary>Gets or sets whether profiles may enable query rewriting by default.</summary>
    /// <value>False by default: query rewriting is off unless a profile enables it.</value>
    public bool EnableQueryRewriting { get; set; }

    /// <summary>Gets or sets whether each retrieval result must be authorized for model exposure by default.</summary>
    /// <value>True by default. A profile may only turn it on, never off, when the host keeps this true.</value>
    public bool RequireExposureAuthorization { get; set; } = true;

    /// <summary>Gets or sets the lifetime of each single-use grant the coordinator and pipeline request.</summary>
    /// <value>A positive duration. The default is 30 seconds.</value>
    public TimeSpan GrantLifetime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the estimated UTF-8 bytes per token the default budget policy uses.</summary>
    /// <value>A positive value. The default is 4.0.</value>
    public double EstimatedBytesPerToken { get; set; } = 4.0;

    /// <summary>Gets or sets the ceiling of semantic-operation (embedding and reranking) requests one operation may reserve.</summary>
    /// <value>A positive bound. The default is 8.</value>
    public int MaximumSemanticRequestsPerOperation { get; set; } = 8;
}
