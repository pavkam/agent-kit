// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Conversation-local helpers for resolving hosted agents.</summary>
internal static class AgentResolutionExtensions
{
    /// <summary>Requires a resolved agent handle.</summary>
    /// <param name="resolution">The engine resolution outcome.</param>
    /// <returns>The runnable handle.</returns>
    /// <exception cref="InvalidOperationException">The agent is missing or invalid.</exception>
    internal static Agent RequireResolved(this AgentResolution resolution) => resolution switch
    {
        ResolvedAgent resolved => resolved.Agent,
        AgentNotFound notFound => throw new InvalidOperationException($"The agent '{notFound.AgentId}' is not hosted by this engine."),
        InvalidAgent invalid => throw new InvalidOperationException(
            $"The agent '{invalid.AgentId}' is unusable: {string.Join("; ", invalid.Diagnostics.Select(static diagnostic => diagnostic.SafeMessage))}"),
        _ => throw new InvalidOperationException($"Unrecognized {nameof(AgentResolution)} kind '{resolution.GetType()}'."),
    };
}
