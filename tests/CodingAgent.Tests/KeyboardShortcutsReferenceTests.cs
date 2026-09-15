// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class KeyboardShortcutsReferenceTests
{
    [Fact]
    public void Markdown_WhenRead_DescribesEveryApplicationShortcutGroup()
    {
        var markdown = KeyboardShortcutsReference.Markdown;

        markdown.ShouldContain("## Write and send");
        markdown.ShouldContain("## Command palette");
        markdown.ShouldContain("## Transcript navigation");
        markdown.ShouldContain("## Approvals and questions");
        markdown.ShouldContain("## Windows and application");
        markdown.ShouldContain("`Shift+Enter`");
        markdown.ShouldContain("`Ctrl+Q`");
    }
}
