// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using System.Text;
using System.Text.Json.Nodes;

/// <summary>
/// Projects a <see cref="RuntimeMessage"/>'s content into the one canonical
/// tagged, non-instruction-bearing wire representation every first-party
/// conversational request translator uses.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RuntimeMessage"/> carries synthetic in-run operational
/// evidence such as a dangling-tool-call recovery notice or a compaction
/// checkpoint summary. It is never a system or developer instruction, but
/// most first-party wire protocols have no distinct "runtime" role: their
/// role vocabulary is limited to roles such as <c>system</c>/<c>developer</c>,
/// <c>user</c>, <c>assistant</c>, and <c>tool</c>. Every translator that
/// lacks a native runtime role sends the message under whichever
/// non-instruction role its protocol already uses for ordinary user-turn
/// content (Anthropic, AWS Bedrock, Cohere, Google Gemini, Mistral AI, and
/// OpenAI-compatible chat completions all use <c>user</c>), and wraps the
/// text in the envelope this type builds so the payload is distinguishable
/// from a genuine user message on the wire, even though the surrounding
/// role name is shared with real user turns.
/// </para>
/// <para>
/// The envelope is a small JSON object with a stable <c>format</c> marker and
/// the notice's joined text under <c>content</c>:
/// </para>
/// <code>
/// {"format":"agentkit.runtime-message.v1","content":"The run was interrupted."}
/// </code>
/// <para>
/// This type does not decide where the envelope text is placed in a
/// provider's wire message (a single text content block, a bare string
/// content field, and so on); that remains each translator's responsibility,
/// because content-part nesting is wire-format specific. It also never
/// promotes the notice to a system, developer, or other instruction-bearing
/// role; callers remain responsible for choosing a non-instruction role.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class RuntimeMessageProjection
{
    /// <summary>
    /// The stable envelope format marker every first-party translator emits
    /// for a projected <see cref="RuntimeMessage"/>. Consumers can use this
    /// value to detect the envelope without hard-coding the literal string.
    /// </summary>
    public const string Format = "agentkit.runtime-message.v1";

    /// <summary>
    /// Builds the canonical tagged envelope JSON for a <see cref="RuntimeMessage"/>'s content parts.
    /// </summary>
    /// <param name="parts">
    /// The runtime message's content parts. Every part must be a <see cref="TextPart"/>; a default or empty
    /// array produces an envelope with empty <c>content</c>.
    /// </param>
    /// <returns>
    /// A compact JSON object string with exactly two properties, in this order: <c>format</c> set to
    /// <see cref="Format"/>, and <c>content</c> set to the concatenation of every part's text in source order.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="parts"/> contains a content part that is not a <see cref="TextPart"/>. Runtime notices
    /// are framework-authored plain text; a non-text part would silently lose information no translator can
    /// recover here.
    /// </exception>
    public static string BuildEnvelopeJson(ImmutableArray<ContentPart> parts) =>
        new JsonObject
        {
            ["format"] = Format,
            ["content"] = JoinText(parts),
        }.ToJsonString();

    private static string JoinText(ImmutableArray<ContentPart> parts)
    {
        var builder = new StringBuilder();
        var normalizedParts = parts.IsDefault ? [] : parts;

        foreach (var part in normalizedParts)
        {
            _ = part switch
            {
                TextPart text => builder.Append(text.Text),
                _ => throw new NotSupportedException(
                    $"Content part kind '{part.GetType().Name}' is not supported in a runtime message by " +
                    $"{nameof(RuntimeMessageProjection)}: runtime notices are framework-authored plain text."),
            };
        }

        return builder.ToString();
    }
}
