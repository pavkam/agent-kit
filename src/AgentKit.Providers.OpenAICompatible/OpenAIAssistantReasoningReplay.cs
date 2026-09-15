// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// Selects how a prior assistant turn's <see cref="ReasoningPart"/> content is replayed to an
/// OpenAI-compatible Chat Completions endpoint when the conversation history is translated into a
/// request.
/// </summary>
/// <remarks>
/// <para>
/// Reasoning dialects diverge: OpenAI's own Chat Completions surface never returns reasoning text
/// on the assistant message and rejects unknown message members, while DeepSeek and Moonshot Kimi
/// thinking models return it on a sibling <c>reasoning_content</c> field and expect it echoed
/// back in later requests of the same tool-call loop. The selected mode is part of the tested
/// <see cref="OpenAICompatibilityProfile"/> so each branded package states the behavior its
/// endpoint actually verified rather than relying on a shared guess.
/// </para>
/// <para>
/// Every mode keeps the <see cref="ReasoningPart"/> in AgentKit's own history untouched. Replay
/// only affects the provider-facing projection of that history.
/// </para>
/// </remarks>
public enum OpenAIAssistantReasoningReplay
{
    /// <summary>
    /// Drop reasoning parts from replayed assistant messages. Visible text and tool calls are still
    /// translated. Use this for endpoints that reject or ignore an assistant-side
    /// <c>reasoning_content</c> member.
    /// </summary>
    Omit,

    /// <summary>
    /// Emit the concatenated visible reasoning text of the assistant message as its
    /// <c>reasoning_content</c> member, alongside <c>content</c> and <c>tool_calls</c>. Reasoning
    /// parts without visible text contribute nothing, and an assistant message without visible
    /// reasoning carries no <c>reasoning_content</c> member at all.
    /// </summary>
    ReasoningContentField,
}
