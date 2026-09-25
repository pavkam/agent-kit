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

    public double RelevanceScore { get; set; }
}

internal sealed class CohereRerankMetaDto
{
    public CohereRerankBillingDto? BilledUnits { get; set; }
}

internal sealed class CohereRerankBillingDto
{
    public int? SearchUnits { get; set; }
}
