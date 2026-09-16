// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies DirectoryEntry behavior and contracts.</summary>
public sealed class DirectoryEntryTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");
        var entry = new DirectoryEntry(path);
        entry.Path.ShouldBe(path);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DirectoryEntry(new FileSystemPath("a.txt"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
