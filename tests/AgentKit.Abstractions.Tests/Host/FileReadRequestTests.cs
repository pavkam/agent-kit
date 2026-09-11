// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileReadRequest behavior and contracts.</summary>
public sealed class FileReadRequestTests
{
    [Fact]
    public void FileReadRequest_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");
        var request = new FileReadRequest(path, SecurityTestData.Grant());
        request.Path.ShouldBe(path);
    }

    [Fact]
    public void FileReadRequest_Equality_WhenSamePath_InstancesAreEqual()
    {
        var path = new FileSystemPath("a.txt");
        new FileReadRequest(path, SecurityTestData.Grant()).ShouldBe(new FileReadRequest(path, SecurityTestData.Grant()));
    }
}
