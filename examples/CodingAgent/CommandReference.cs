// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Text;

/// <summary>Authors the slash-command reference shown by <c>/help</c> and the Help menu.</summary>
internal static class CommandReference
{
    /// <summary>The dialog title.</summary>
    public const string Title = "Command Reference";

    /// <summary>Builds grouped Markdown from an immutable command catalog.</summary>
    /// <param name="commands">The initialized command catalog to describe.</param>
    /// <returns>A formatted reference containing every command exactly once.</returns>
    /// <exception cref="ArgumentException"><paramref name="commands"/> is its default value.</exception>
    internal static string BuildMarkdown(ImmutableArray<SlashCommand> commands)
    {
        ArgumentException.ThrowIfDefault(commands);
        var markdown = new StringBuilder(
            "# Slash commands\n\n> Type a command in the composer and press `Enter`. Commands run locally; they are not sent to the model.\n");

        foreach (var section in commands.GroupBy(static command => command.Section))
        {
            _ = markdown.Append("\n## ").Append(section.Key).Append('\n');
            foreach (var command in section)
            {
                _ = markdown
                    .Append("\n### `").Append(command.Syntax).Append("`\n\n")
                    .Append("**").Append(command.Description).Append("** ")
                    .Append(command.Details).Append('\n');
            }
        }

        _ = markdown.Append("\n---\n\n**Tip:** Press `/` or `Ctrl+K` to search application actions without memorizing command names.\n");
        return markdown.ToString();
    }
}
