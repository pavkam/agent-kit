// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileNotFound behavior and contracts.</summary>
public sealed class FileNotFoundTests
{
    [Fact]
    public void FileNotFound_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("missing.txt");
        var result = new FileNotFound(path);
        result.Path.ShouldBe(path);
    }
}
