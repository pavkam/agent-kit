// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// Configures the Amazon Bedrock Runtime integration's region and
/// wire-behavior defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddAwsBedrockStaticCredential</c> or a custom
/// <see cref="IAwsCredentialSource"/> registration, because credential
/// material is never appropriate to bind from ordinary configuration
/// alongside endpoint options. A Bedrock Runtime endpoint is
/// region-specific, so <see cref="Region"/> has no fabricated default; it
/// must be configured explicitly.
/// </remarks>
public sealed class AwsBedrockProviderOptions
{
    /// <summary>
    /// Gets or sets the AWS region hosting the request, such as
    /// <c>us-east-1</c>, or <see langword="null"/> if not yet configured.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>ConverseStream</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;
}
