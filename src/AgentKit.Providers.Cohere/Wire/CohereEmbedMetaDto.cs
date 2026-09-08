// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>meta</c> object in a Cohere v2 embed response.
/// </summary>
internal sealed class CohereEmbedMetaDto
{
    /// <summary>Gets or sets billable-unit accounting for the request.</summary>
    [JsonPropertyName("billed_units")]
    public CohereEmbedBilledUnitsDto? BilledUnits { get; set; }
}
