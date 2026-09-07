// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

public sealed class FileReadResultsTests
{
    [Fact]
    public void FileReadRequest_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");

        var request = new FileReadRequest(path);

        request.Path.ShouldBe(path);
    }

    [Fact]
    public void FileReadRequest_Equality_WhenSamePath_InstancesAreEqual()
    {
        var path = new FileSystemPath("a.txt");

        new FileReadRequest(path).ShouldBe(new FileReadRequest(path));
    }

    [Fact]
    public void FileRead_Constructor_WhenContentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new FileRead(null!, 0));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void FileRead_Constructor_WhenBytesNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileRead("text", -1));

        exception.ParamName.ShouldBe("bytes");
    }

    [Fact]
    public void FileRead_Constructor_WhenValid_RoundTripsProperties()
    {
        var read = new FileRead("text", 4);

        read.Content.ShouldBe("text");
        read.Bytes.ShouldBe(4);
    }

    [Fact]
    public void FileRead_Equality_WhenSameValues_InstancesAreEqual() => new FileRead("text", 4).ShouldBe(new FileRead("text", 4));

    [Fact]
    public void FileRead_Equality_WhenDifferentContent_InstancesAreNotEqual() => new FileRead("text", 4).ShouldNotBe(new FileRead("other", 4));

    [Fact]
    public void FileNotFound_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("missing.txt");

        var result = new FileNotFound(path);

        result.Path.ShouldBe(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileReadDenied_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileReadDenied(safeMessage!));

    [Fact]
    public void FileReadDenied_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var denied = new FileReadDenied("outside sandbox");

        denied.SafeMessage.ShouldBe("outside sandbox");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileReadFailed_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileReadFailed(safeMessage!));

    [Fact]
    public void FileReadFailed_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var failed = new FileReadFailed("disk error");

        failed.SafeMessage.ShouldBe("disk error");
    }

    [Fact]
    public void FileReadResult_Hierarchy_EveryLeafDerivesFromFileReadResult()
    {
        FileReadResult read = new FileRead("x", 1);
        FileReadResult notFound = new FileNotFound(new FileSystemPath("a.txt"));
        FileReadResult denied = new FileReadDenied("no");
        FileReadResult failed = new FileReadFailed("no");

        _ = read.ShouldBeOfType<FileRead>();
        _ = notFound.ShouldBeOfType<FileNotFound>();
        _ = denied.ShouldBeOfType<FileReadDenied>();
        _ = failed.ShouldBeOfType<FileReadFailed>();
    }
}
