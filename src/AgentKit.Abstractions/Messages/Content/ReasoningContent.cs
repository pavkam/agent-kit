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
/// <para>
/// <see cref="Text"/>, <see cref="Visibility"/>, and
/// <see cref="SignatureToken"/> must agree with one another, so they are
/// read-only rather than <see langword="init"/> members: a <c>with</c>
/// expression cannot change one without the others, and callers construct a
/// new value instead.
/// </para>
/// </remarks>
public sealed record ReasoningContent
{
    /// <summary>Initializes a new instance of the <see cref="ReasoningContent"/> record.</summary>
    /// <param name="text">
    /// The visible reasoning text when <paramref name="visibility"/> is
    /// <see cref="ReasoningVisibility.Visible"/>; must be <see langword="null"/>
    /// for every other visibility. A visible segment may still carry
    /// <see langword="null"/> when the provider emitted a thought block without
    /// text, which consumers treat as absent text rather than as redaction.
    /// </param>
    /// <param name="visibility">How much of this reasoning is observable outside the provider; must be a defined <see cref="ReasoningVisibility"/>.</param>
    /// <param name="signatureToken">
    /// An opaque provider signature reused only on a compatible
    /// continuation path, when the provider supplies one. Required and
    /// nonblank when <paramref name="visibility"/> is
    /// <see cref="ReasoningVisibility.EncryptedSignature"/>, because that
    /// visibility has no other representation.
    /// </param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="visibility"/> is not a defined <see cref="ReasoningVisibility"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> is non-null while <paramref name="visibility"/> is not
    /// <see cref="ReasoningVisibility.Visible"/>, or <paramref name="signatureToken"/> is null, empty, or
    /// whitespace while <paramref name="visibility"/> is <see cref="ReasoningVisibility.EncryptedSignature"/>.
    /// </exception>
    public ReasoningContent(
        string? text,
        ReasoningVisibility visibility,
        string? signatureToken,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(visibility);
        if (visibility is not ReasoningVisibility.Visible)
        {
            ArgumentException.ThrowIfNotEqual(text, null, nameof(text));
        }

        if (visibility is ReasoningVisibility.EncryptedSignature)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(signatureToken);
        }

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
    /// <value>Always null for redacted or signature-only reasoning; possibly null for a visible segment without text.</value>
    public string? Text { get; }

    /// <summary>
    /// Gets how much of this reasoning is observable outside the provider.
    /// </summary>
    /// <value>A defined <see cref="ReasoningVisibility"/> that governs <see cref="Text"/> and <see cref="SignatureToken"/>.</value>
    public ReasoningVisibility Visibility { get; }

    /// <summary>
    /// Gets an opaque provider signature reused only on a compatible
    /// continuation path, when the provider supplies one.
    /// </summary>
    /// <value>Nonblank for <see cref="ReasoningVisibility.EncryptedSignature"/>; otherwise optional.</value>
    public string? SignatureToken { get; }

    /// <summary>Gets provider-specific or forward-compatible data.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
