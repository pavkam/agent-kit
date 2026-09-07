// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>usage</c> object in a buffered
/// <c>ChatResponse</c> or the <c>message-end</c> streaming event.
/// </summary>
/// <remarks>
/// Cohere reports two distinct usage views, <c>tokens</c> (raw token
/// accounting) and <c>billed_units</c> (billable units, which can differ
/// from raw counts). This package translates only <c>tokens</c> and
/// <c>cached_tokens</c> into <see cref="ModelUsage"/>; <c>billed_units</c>
/// is a separate billing view this package does not yet surface.
/// </remarks>
internal sealed class CohereUsageDto
{
    /// <summary>Gets or sets the raw input/output token accounting.</summary>
    [JsonPropertyName("tokens")]
    public CohereUsageTokensDto? Tokens { get; set; }

    /// <summary>Gets or sets the number of prompt tokens that hit the inference cache.</summary>
    [JsonPropertyName("cached_tokens")]
    public double? CachedTokens { get; set; }
}
