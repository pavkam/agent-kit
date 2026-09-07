// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// A unified deserialization envelope covering every
/// <c>ConverseStream</c> event payload shape this package translates
/// (<c>messageStart</c>, <c>contentBlockStart</c>,
/// <c>contentBlockDelta</c>, <c>contentBlockStop</c>, <c>messageStop</c>,
/// <c>metadata</c>) and the exception frame payload shape. The frame's
/// <c>:event-type</c>/<c>:message-type</c> reserved headers, not any field
/// on this type, determine which properties are populated for a given
/// event; unused properties remain <see langword="null"/>.
/// </summary>
internal sealed class AwsBedrockStreamEventDto
{
    /// <summary>Gets or sets the message role, present on a <c>messageStart</c> event.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the content block index, present on
    /// <c>contentBlockStart</c>, <c>contentBlockDelta</c>, and
    /// <c>contentBlockStop</c> events.
    /// </summary>
    [JsonPropertyName("contentBlockIndex")]
    public int? ContentBlockIndex { get; set; }

    /// <summary>Gets or sets the content block opening payload, present on a <c>contentBlockStart</c> event.</summary>
    [JsonPropertyName("start")]
    public AwsBedrockContentBlockStartDto? Start { get; set; }

    /// <summary>Gets or sets the content block delta payload, present on a <c>contentBlockDelta</c> event.</summary>
    [JsonPropertyName("delta")]
    public AwsBedrockContentBlockDeltaDto? Delta { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason, present on a <c>messageStop</c> event.</summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; set; }

    /// <summary>Gets or sets token usage, present on a <c>metadata</c> event.</summary>
    [JsonPropertyName("usage")]
    public AwsBedrockTokenUsageDto? Usage { get; set; }

    /// <summary>Gets or sets a human-readable diagnostic message, present on an exception frame.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
