// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Configures the durable-memory and retrieval profile that <c>WithMemory</c> publishes for every hosted agent.</summary>
/// <remarks>
/// The profile is deliberately narrow: an ephemeral in-memory store and keyword retrieval over active memories. Embeddings, document
/// stores, vector indexes, and reranking are composed on <see cref="AgentEngineBuilder.Services"/> with the memory package's own
/// registrations and a hand-written profile.
/// </remarks>
public sealed class SimpleMemoryOptions
{
    /// <summary>Gets or sets the highest data classification the profile may retain or expose to the model.</summary>
    /// <value>A defined classification. The default is <see cref="DataClassification.Internal"/>.</value>
    public DataClassification MaximumClassification { get; set; } = DataClassification.Internal;

    /// <summary>Gets or sets a value indicating whether a proposal is accepted whenever no registered memory policy denies it.</summary>
    /// <value>
    /// <see langword="false"/> (the default) keeps the fail-closed rule: a proposal is retained only when a registered policy
    /// explicitly allows it. <see langword="true"/> switches the engine to accept unless a policy denies, which suits local
    /// development but lets any caller that can propose a memory retain it.
    /// </value>
    public bool AcceptProposals { get; set; }
}
