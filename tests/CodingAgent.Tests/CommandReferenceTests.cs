// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class CommandReferenceTests
{
    [Fact]
    public void BuildMarkdown_WhenCatalogProvided_DescribesEveryAcceptedCommand()
    {
        var markdown = CommandReference.BuildMarkdown(SlashCommands.All);

        markdown.ShouldContain("## Sessions and transcript");
        markdown.ShouldContain("## Agent and workspace");
        markdown.ShouldContain("## Help and application");
        foreach (var command in SlashCommands.All)
        {
            markdown.ShouldContain($"### `{command.Syntax}`");
            markdown.ShouldContain(command.Description);
            markdown.ShouldContain(command.Details);
        }
    }
}
