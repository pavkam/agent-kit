// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One model reasoning/thinking segment, including redacted or
/// signature-only provider forms, carried by a <see cref="ReasoningPart"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Reasoning content is provider-sensitive: some providers return the full
/// reasoning text, some redact it while still charging for it, and some
/// return only an opaque, verifiable signature that must be echoed back on
/// a follow-up request to reuse cached reasoning. <see cref="Visibility"/>
/// makes that distinction explicit so a context assembler or provider
/// adapter never mistakes a redacted or signature-only segment for visible
/// text it can safely display or summarize.
/// </para>
/// </remarks>
public sealed record ReasoningContent
{
    /// <summary>Initializes a new instance of the <see cref="ReasoningContent"/> record.</summary>
    /// <param name="text">
    /// The visible reasoning text, when <paramref name="visibility"/> is
    /// <see cref="ReasoningVisibility.Visible"/>; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <param name="visibility">How much of this reasoning is observable outside the provider.</param>
    /// <param name="signatureToken">
    /// An opaque provider signature reused only on a compatible
    /// continuation path, when the provider supplies one.
    /// </param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ReasoningContent(
        string? text,
        ReasoningVisibility visibility,
        string? signatureToken,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Text = text;
        Visibility = visibility;
        SignatureToken = signatureToken;
        Extensions = extensions;
    }

    /// <summary>
    /// Gets the visible reasoning text, when <see cref="Visibility"/> is
    /// <see cref="ReasoningVisibility.Visible"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets how much of this reasoning is observable outside the provider.
    /// </summary>
    public ReasoningVisibility Visibility { get; init; }

    /// <summary>
    /// Gets an opaque provider signature reused only on a compatible
    /// continuation path, when the provider supplies one.
    /// </summary>
    public string? SignatureToken { get; init; }

    /// <summary>Gets provider-specific or forward-compatible data.</summary>
    public ExtensionData Extensions { get; init; }
}
