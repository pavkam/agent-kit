// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileChange behavior and contracts.</summary>
public sealed class FileChangeTests
{
    [Fact]
    public void Constructor_WhenRelativePathDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileChange(default, FileChangeKind.Modified));
        exception.ParamName.ShouldBe("relativePath");
    }

    [Fact]
    public void Constructor_WhenKindUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new FileChange(new NormalizedRelativePath("a.txt"), (FileChangeKind) 999));
        exception.ParamName.ShouldBe("kind");
    }
}
