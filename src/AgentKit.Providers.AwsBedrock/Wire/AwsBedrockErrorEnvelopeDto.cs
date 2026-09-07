// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of a Bedrock Runtime JSON error body, following the
/// standard AWS <c>restJson1</c> protocol error envelope. The exception
/// name itself is carried out-of-band on the <c>x-amzn-errortype</c>
/// response header, not in this body.
/// </summary>
internal sealed class AwsBedrockErrorEnvelopeDto
{
    /// <summary>Gets or sets the human-readable diagnostic message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
