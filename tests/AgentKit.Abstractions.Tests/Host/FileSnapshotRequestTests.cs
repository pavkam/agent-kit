// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSnapshotRequest behavior and contracts.</summary>
public sealed class FileSnapshotRequestTests
{
    [Fact]
    public void FileSnapshotRequest_WhenMaximumBytesIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSnapshotRequest(new FileSystemPath("a.txt"), 0, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("maximumBytes");
    }

    [Fact]
    public void FileSnapshotRequest_WhenGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new FileSnapshotRequest(new FileSystemPath("a.txt"), 100, null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void FileSnapshotRequest_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var path = new FileSystemPath("a.txt");
        var grant = SecurityTestData.Grant();
        var request = new FileSnapshotRequest(path, 100, grant);
        request.Path.ShouldBe(path);
        request.MaximumBytes.ShouldBe(100);
        request.Grant.ShouldBeSameAs(grant);
    }

    [Fact]
    public void FileSnapshotRequest_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new FileSnapshotRequest(new FileSystemPath("a.txt"), 100, SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
