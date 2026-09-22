// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies NormalizedRelativePath behavior and contracts.</summary>
public sealed class NormalizedRelativePathTests
{
    [Fact]
    public void Constructor_WhenValueRooted_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new NormalizedRelativePath("/etc/passwd"));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueContainsTraversal_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new NormalizedRelativePath("sub/../secret.txt"));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueValid_NormalizesSegments()
    {
        var path = new NormalizedRelativePath("./sub\\dir/./file.txt");
        path.Value.ShouldBe("sub/dir/file.txt");
    }
}
