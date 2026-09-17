// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of an OpenAI-compatible error response body.
/// </summary>
internal sealed class OpenAIErrorResponse
{
    /// <summary>Gets or sets the nested error detail.</summary>
    /// <remarks>
    /// Most OpenAI-compatible servers send this as an object. xAI sends it as a bare string instead,
    /// with its <see cref="TopLevelCode"/> carried as a sibling of <c>error</c> rather than nested
    /// inside it; <see cref="FlexibleErrorDetailJsonConverter"/> accepts either wire form.
    /// </remarks>
    [JsonPropertyName("error")]
    [JsonConverter(typeof(FlexibleErrorDetailJsonConverter))]
    public OpenAIErrorDetail? Error { get; set; }

    /// <summary>Gets or sets the provider's top-level, machine-readable error code, when supplied outside <see cref="Error"/>.</summary>
    /// <remarks>
    /// xAI's error shape (<c>{"code":"&lt;status text&gt;","error":"&lt;message&gt;"}</c>) carries this
    /// as a top-level sibling of <c>error</c> instead of nesting it under the common
    /// <see cref="OpenAIErrorDetail.Code"/> member.
    /// </remarks>
    [JsonPropertyName("code")]
    [JsonConverter(typeof(FlexibleErrorCodeJsonConverter))]
    public string? TopLevelCode { get; set; }
}
