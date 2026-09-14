// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Describes one slash command the chat prompt recognizes instead of sending it to the agent.</summary>
internal sealed record SlashCommand(string Name, string Description);

/// <summary>The fixed set of slash commands this example supports.</summary>
internal static class SlashCommands
{
    /// <summary>Every recognized command, in the order <c>/help</c> lists them.</summary>
    public static readonly ImmutableArray<SlashCommand> All =
    [
        new SlashCommand("/help", "List available commands."),
        new SlashCommand("/clear", "Clear the transcript (keeps the current session)."),
        new SlashCommand("/new", "Start a brand-new session, discarding conversation history."),
        new SlashCommand("/model", "Show the configured model id."),
        new SlashCommand("/workspace", "Show the workspace root this agent is scoped to."),
        new SlashCommand("/quit", "Exit CodingAgent."),
    ];

    /// <summary>Finds every command whose name starts with <paramref name="prefix"/>.</summary>
    /// <param name="prefix">The text typed so far, including the leading slash.</param>
    /// <returns>Matching commands, in <see cref="All"/> order.</returns>
    public static IEnumerable<SlashCommand> Match(string prefix) =>
        All.Where(command => command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
