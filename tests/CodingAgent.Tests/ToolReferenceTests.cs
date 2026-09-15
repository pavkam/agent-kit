// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class ToolReferenceTests
{
    [Theory]
    [InlineData(0, "Ask before")]
    [InlineData(1, "Deny writes")]
    [InlineData(2, "Allow workspace writes")]
    public void BuildMarkdown_WhenModeProvided_ListsEveryToolAndLivePermission(
        int permissionMode,
        string expectedPermission)
    {
        var mode = (PermissionMode) permissionMode;

        var markdown = ToolReference.BuildMarkdown(mode);

        foreach (var tool in new[] { "read_file", "glob", "search", "write_file", "edit", "command", "plan", "todo", "question" })
        {
            markdown.ShouldContain($"`{tool}`");
        }

        markdown.ShouldContain(expectedPermission);
        markdown.ShouldContain(PermissionModeCatalog.Title(mode));
        markdown.ShouldContain("network blocking");
    }
}
