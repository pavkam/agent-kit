// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Retrieval;

/// <summary>Configures how the retrieval context contributor builds queries and publishes candidates.</summary>
/// <remarks>Every ceiling is requested, not granted: the selected memory profile and the engine narrow it further, and a value above the profile's ceiling causes the retrieval to be refused rather than silently lowered.</remarks>
public sealed class RetrievalContextOptions
{
    /// <summary>Gets or sets the highest data classification the contributor asks retrieval to expose to the model.</summary>
    /// <value>A defined classification. The default is <see cref="DataClassification.Internal"/>.</value>
    public DataClassification MaximumClassification { get; set; } = DataClassification.Internal;

    /// <summary>Gets or sets the maximum number of candidates requested per model request.</summary>
    /// <value>A positive count. The default is 8.</value>
    public int MaximumItems { get; set; } = 8;

    /// <summary>Gets or sets the maximum total UTF-8 bytes of candidate text requested per model request.</summary>
    /// <value>A positive byte count. The default is 32768.</value>
    public int MaximumBytes { get; set; } = 32_768;

    /// <summary>Gets or sets the maximum estimated tokens of candidate text requested per model request.</summary>
    /// <value>A positive token count. The default is 4096.</value>
    public int MaximumTokens { get; set; } = 4_096;

    /// <summary>Gets or sets the maximum number of characters of the latest user message used as the query.</summary>
    /// <value>A positive count. Longer text is truncated at this bound. The default is 2000.</value>
    public int MaximumQueryCharacters { get; set; } = 2_000;

    /// <summary>Gets or sets the priority assigned to the best candidate; lower-ranked candidates decrease from it.</summary>
    /// <value>Any integer. The default is 40.</value>
    public int Priority { get; set; } = 40;
}
