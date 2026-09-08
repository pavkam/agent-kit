// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>meta.billed_units</c> object in a Cohere v2
/// embed response.
/// </summary>
internal sealed class CohereEmbedBilledUnitsDto
{
    /// <summary>Gets or sets the number of billable input tokens.</summary>
    [JsonPropertyName("input_tokens")]
    public double? InputTokens { get; set; }
}
