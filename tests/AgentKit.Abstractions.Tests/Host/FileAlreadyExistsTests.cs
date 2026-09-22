// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies LegacyFileAlreadyExists behavior and contracts.</summary>
public sealed class FileAlreadyExistsTests
{
    [Fact]
    [Obsolete("Legacy host surface.")]

    public void FileAlreadyExists_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");
        var result = new LegacyFileAlreadyExists(path);
        result.Path.ShouldBe(path);
    }

    [Fact]
    [Obsolete("Legacy host surface.")]

    public void FileAlreadyExists_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LegacyFileAlreadyExists(new FileSystemPath("a.txt"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
