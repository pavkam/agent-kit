// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// Resolves the wire identifier each <see cref="ToolCallId"/> in a
/// conversation is sent under, so request translators correlate tool calls
/// and their results by the identifier the provider originally issued.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ToolCallPart"/> that came back from a provider carries that
/// provider's own call identifier as <see cref="ToolCallPart.ProviderCallId"/>;
/// replaying the turn must echo exactly that value or the provider will not
/// match the result to its call. A tool call that was minted locally has no
/// provider identifier and is sent under its canonical
/// <see cref="ToolCallId"/> text instead. Providers that impose their own
/// identifier alphabet (for example Mistral's nine-character format) own a
/// separate codec and do not use this map.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class ProviderToolCallIds
{
    /// <summary>
    /// Collects the wire identifier for every tool call in
    /// <paramref name="messages"/>, in conversation order.
    /// </summary>
    /// <param name="messages">The conversation to scan. A default or empty array yields an empty map.</param>
    /// <returns>
    /// A mutable map from each <see cref="ToolCallPart.CallId"/> to its
    /// <see cref="ProviderToolCallId.Value"/> when the part has one, or to the
    /// canonical <see cref="ToolCallId"/> text otherwise. When the same call
    /// identity appears more than once, the last occurrence wins. The caller
    /// owns the returned dictionary.
    /// </returns>
    public static Dictionary<ToolCallId, string> Collect(ImmutableArray<AgentMessage> messages)
    {
        var map = new Dictionary<ToolCallId, string>();
        if (messages.IsDefaultOrEmpty)
        {
            return map;
        }

        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                if (part is ToolCallPart toolCall)
                {
                    map[toolCall.CallId] = toolCall.ProviderCallId?.Value ?? toolCall.CallId.ToString();
                }
            }
        }

        return map;
    }
}
