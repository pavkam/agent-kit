// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileAlreadyExists behavior and contracts.</summary>
public sealed class FileAlreadyExistsTests
{
    [Fact]
    public void FileAlreadyExists_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");
        var result = new FileAlreadyExists(path);
        result.Path.ShouldBe(path);
    }
}
