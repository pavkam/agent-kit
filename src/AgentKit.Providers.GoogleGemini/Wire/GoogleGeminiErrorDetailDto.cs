// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of the nested <c>error</c> object in a Google
/// <c>google.rpc.Status</c>-style error envelope.
/// </summary>
/// <remarks>
/// Only the three fields every Google surface emits are modeled. Typed
/// <c>details</c> entries are intentionally not bound: their shape varies
/// per service and they never influence the normalized failure kind. The
/// <see cref="Message"/> is untrusted provider prose and must be kept out of
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
}
