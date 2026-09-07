// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of the nested <c>error</c> object in a Google
/// <c>google.rpc.Status</c>-style error envelope.
/// </summary>
internal sealed class GoogleGeminiErrorDetailDto
{
    /// <summary>Gets or sets the numeric gRPC-mapped HTTP status code.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    /// <summary>Gets or sets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets Google's stable canonical status string, such as
    /// <c>"INVALID_ARGUMENT"</c> or <c>"RESOURCE_EXHAUSTED"</c>.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
