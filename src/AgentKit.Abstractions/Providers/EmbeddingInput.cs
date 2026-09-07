// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one input to embed, carried within an
/// <see cref="EmbeddingRequest"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a new kind.
/// </para>
/// <para>
/// This is a deliberately minimal input inventory covering only text
/// input, the one portable input kind every embedding provider this
/// repository targets accepts. Token-ID input is tokenizer-tied and not
/// portable across providers, and image/audio/video/compound input is a
/// capability-gated extension not yet translated by any adapter in this
/// repository; those kinds will be added additively once a concrete
/// adapter needs to translate them, rather than being fabricated here.
/// </para>
/// </remarks>
public abstract record EmbeddingInput
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingInput"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected EmbeddingInput()
    {
    }
}
