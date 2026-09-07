// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of one Cohere v2 Chat streaming event, covering the
/// union of fields used across every event <see cref="Type"/> in the
/// semantic event grammar (<c>message-start</c>, <c>content-start</c>,
/// <c>content-delta</c>, <c>content-end</c>, <c>tool-plan-delta</c>,
/// <c>tool-call-start</c>, <c>tool-call-delta</c>, <c>tool-call-end</c>,
/// <c>citation-start</c>, <c>citation-end</c>, and <c>message-end</c>).
/// Only the fields matching the event's actual kind are populated; the
/// others remain null.
/// </summary>
internal sealed class CohereStreamEventDto
{
    /// <summary>Gets or sets the event-type discriminator.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the reply identifier, present on <c>message-start</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the ordinal position of the content block or tool call
    /// this event pertains to, present on every per-block/per-call event.
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    /// <summary>Gets or sets the event's payload.</summary>
    [JsonPropertyName("delta")]
    public CohereStreamEventDeltaDto? Delta { get; set; }
}
