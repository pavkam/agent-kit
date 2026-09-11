// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies DirectoryEnumerationCursor behavior and contracts.</summary>
public sealed class DirectoryEnumerationCursorTests
{
    [Fact]
    public void DirectoryEnumerationCursor_WhenNextIndexIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DirectoryEnumerationCursor(new ContentHash("sha256:test"), 0));
        exception.ParamName.ShouldBe("nextIndex");
    }
}
