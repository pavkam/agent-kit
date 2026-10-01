// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

internal sealed class CohereRerankResponseDto
{
    public List<CohereRerankResultDto>? Results { get; set; }

    public CohereRerankMetaDto? Meta { get; set; }
}

internal sealed class CohereRerankResultDto
{
    public int Index { get; set; }

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; set; }
}

internal sealed class CohereRerankMetaDto
{
    [JsonPropertyName("billed_units")]
    public CohereRerankBillingDto? BilledUnits { get; set; }
}

internal sealed class CohereRerankBillingDto
{
    [JsonPropertyName("search_units")]
    public int? SearchUnits { get; set; }
}
