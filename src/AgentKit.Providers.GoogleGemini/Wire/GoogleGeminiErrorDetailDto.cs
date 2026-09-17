// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of the nested <c>error</c> object in a Google
/// <c>google.rpc.Status</c>-style error envelope.
/// </summary>
/// <remarks>
/// The four fields modeled here are every Google surface's error envelope
/// members this adapter interprets. Each raw <c>details</c> entry's shape
/// varies per service and is not bound to a typed member; entries are kept
/// as raw JSON so a caller can locate a specific type (for example
/// <c>type.googleapis.com/google.rpc.RetryInfo</c>) without this DTO
/// modeling every possible detail shape. Only the retained
/// <c>google.rpc.RetryInfo</c> detail's <c>retryDelay</c> influences a
/// normalized failure's <see cref="ProviderFailure.RetryAfter"/>; details
/// never influence the normalized failure kind. The <see cref="Message"/>
/// is untrusted provider prose and must be kept out of
/// <see cref="ProviderFailure.SafeMessage"/>.
/// </remarks>
public sealed class GoogleGeminiErrorDetailDto
{
    /// <summary>
    /// Gets or sets the numeric HTTP status code Google mirrors into the
    /// body, or <see langword="null"/> when absent.
    /// </summary>
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message, or
    /// <see langword="null"/> when absent.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets Google's stable canonical status string, such as
    /// <c>"INVALID_ARGUMENT"</c> or <c>"RESOURCE_EXHAUSTED"</c>, or
    /// <see langword="null"/> when absent.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the raw <c>google.rpc.Status.details</c> entries, or
    /// <see langword="null"/> when absent. Each entry's own shape depends on
    /// its <c>@type</c>; the only shape this adapter currently reads is
    /// <c>type.googleapis.com/google.rpc.RetryInfo</c>'s <c>retryDelay</c>.
    /// </summary>
    [JsonPropertyName("details")]
    public List<JsonElement>? Details { get; set; }
}
