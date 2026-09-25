// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter.Wire;

internal sealed class OpenRouterRerankResponseDto
{
    public List<OpenRouterRerankResultDto>? Results { get; set; }
}

internal sealed class OpenRouterRerankResultDto
{
    public int Index { get; set; }

    public double RelevanceScore { get; set; }
}
