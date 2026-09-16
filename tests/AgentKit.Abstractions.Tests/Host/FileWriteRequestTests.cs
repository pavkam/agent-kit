// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileWriteRequest behavior and contracts.</summary>
public sealed class FileWriteRequestTests
{
    [Fact]
    public void FileWriteRequest_Constructor_WhenContentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new FileWriteRequest(new FileSystemPath("a.txt"), null!, FileWriteMode.CreateOrOverwrite, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void FileWriteRequest_Constructor_WhenValid_RoundTripsProperties()
    {
        var path = new FileSystemPath("a.txt");
        var request = new FileWriteRequest(path, "hello", FileWriteMode.Append, SecurityTestData.Grant());
        request.Path.ShouldBe(path);
        request.Content.ShouldBe("hello");
        request.Mode.ShouldBe(FileWriteMode.Append);
    }

    [Fact]
    public void FileWriteRequest_Equality_WhenSameValues_InstancesAreEqual()
    {
        var path = new FileSystemPath("a.txt");
        new FileWriteRequest(path, "hi", FileWriteMode.CreateNew, SecurityTestData.Grant()).ShouldBe(new FileWriteRequest(path, "hi", FileWriteMode.CreateNew, SecurityTestData.Grant()));
    }

    [Fact]
    public void FileWriteRequest_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new FileWriteRequest(new FileSystemPath("a.txt"), "hi", FileWriteMode.CreateNew, SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
