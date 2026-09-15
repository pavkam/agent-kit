// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// The stable <see cref="ExtensionData"/> keys under which this package
/// preserves Gemini GenerateContent wire fields that have no portable
/// first-class field on the owning AgentKit record.
/// </summary>
/// <remarks>
/// <para>
/// These keys are part of the package's public contract: the Gemini parser
/// writes them, the Gemini request translator reads them back, and any
/// host that reuses the shared GenerateContent content semantics (such as
/// <c>AgentKit.Providers.GoogleVertexAI</c>) round-trips the same keys.
/// Session stores, history repair, and other providers are expected to
/// preserve entries under these keys unchanged rather than discard them.
/// </para>
/// <para>
/// This type holds only compile-time constants and carries no state.
/// </para>
/// </remarks>
public static class GoogleGeminiExtensionKeys
{
    /// <summary>
    /// The key under which a part's opaque <c>thoughtSignature</c> is
    /// retained on a <see cref="ToolCallPart"/> or <see cref="TextPart"/>.
    /// The value is a JSON string holding the signature exactly as the
    /// provider returned it.
    /// </summary>
    /// <remarks>
    /// Gemini attaches encrypted thought signatures as metadata on ordinary
    /// content parts, most importantly on <c>functionCall</c> parts and on
    /// the final text part of an answer, and requires each signature to be
    /// echoed back inside its original part on the next request. Reasoning
    /// (<c>thought</c>) parts carry their signature in the portable
    /// <see cref="ReasoningContent.SignatureToken"/> instead of under this
    /// key. Use <see cref="GoogleGeminiThoughtSignature"/> to write and read
    /// the value.
    /// </remarks>
    public const string ThoughtSignature = "gemini.thought_signature";

    /// <summary>
    /// The key under which <c>usageMetadata.toolUsePromptTokenCount</c> is
    /// retained on <see cref="ModelUsage.Extensions"/>. The value is a JSON
    /// integer.
    /// </summary>
    public const string ToolUsePromptTokenCount = "tool_use_prompt_token_count";
}
