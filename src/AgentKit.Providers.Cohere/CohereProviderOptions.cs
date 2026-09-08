// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// Configures the Cohere integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddCohereApiKeyCredential</c> or <c>AddCohereOAuthCredential</c>,
/// because credential material is never appropriate to bind from ordinary
/// configuration alongside endpoint options.
/// </remarks>
public sealed class CohereProviderOptions
{
    /// <summary>Gets or sets the base address of the Cohere API.</summary>
    public Uri BaseAddress { get; set; } = CohereProviderDefaults.DefaultBaseAddress;

    /// <summary>
    /// Gets or sets the path, relative to <see cref="BaseAddress"/>, of the
    /// chat operation.
    /// </summary>
    public string ChatPath { get; set; } = CohereProviderDefaults.DefaultChatPath;

    /// <summary>
    /// Gets or sets the path, relative to <see cref="BaseAddress"/>, of the
    /// embed operation.
    /// </summary>
    public string EmbedPath { get; set; } = CohereProviderDefaults.DefaultEmbedPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming chat
    /// operation when the selected model supports streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional application identifier sent as the
    /// <c>X-Client-Name</c> header, or <see langword="null"/> to omit it.
    /// </summary>
    public string? ClientName { get; set; }
}
