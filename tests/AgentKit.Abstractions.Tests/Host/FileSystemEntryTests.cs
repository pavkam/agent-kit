// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileSystemEntry behavior and contracts.</summary>
public sealed class FileSystemEntryTests
{
    [Fact]
    public void Constructor_WhenNameDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemEntry(default, isDirectory: false));
        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void Constructor_WhenValid_DoesNotThrow()
    {
        var entry = new FileSystemEntry(new NormalizedRelativePath("child.txt"), isDirectory: false);
        entry.Name.ShouldBe(new NormalizedRelativePath("child.txt"));
        entry.IsDirectory.ShouldBeFalse();
    }
}
