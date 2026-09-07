// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>usage.tokens</c> object, reporting raw token
/// accounting (as distinct from <c>usage.billed_units</c>, which reports
/// billable units and is not translated by this package).
/// </summary>
internal sealed class CohereUsageTokensDto
{
    /// <summary>Gets or sets the number of tokens used as input to the model.</summary>
    [JsonPropertyName("input_tokens")]
    public double? InputTokens { get; set; }

    /// <summary>Gets or sets the number of tokens produced by the model.</summary>
    [JsonPropertyName("output_tokens")]
    public double? OutputTokens { get; set; }
}
