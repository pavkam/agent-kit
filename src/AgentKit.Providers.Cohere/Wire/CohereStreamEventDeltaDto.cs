// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>delta</c> object carried by a streaming event.
/// Only the fields matching the event's actual kind are populated; the
/// others remain null.
/// </summary>
internal sealed class CohereStreamEventDeltaDto
{
    /// <summary>Gets or sets the message fragment this event carries.</summary>
    [JsonPropertyName("message")]
    public CohereStreamMessageDto? Message { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized finish reason, present only on <c>message-end</c>.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    /// <summary>Gets or sets token usage, present only on <c>message-end</c>.</summary>
    [JsonPropertyName("usage")]
    public CohereUsageDto? Usage { get; set; }

    /// <summary>
    /// Gets or sets an error message if generation failed, present only on
    /// <c>message-end</c>.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
