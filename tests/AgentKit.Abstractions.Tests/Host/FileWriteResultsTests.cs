// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

public sealed class FileWriteResultsTests
{
    [Fact]
    public void FileWriteRequest_Constructor_WhenContentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new FileWriteRequest(
                new FileSystemPath("a.txt"), null!, FileWriteMode.CreateOrOverwrite, SecurityTestData.Grant()));

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

        new FileWriteRequest(path, "hi", FileWriteMode.CreateNew, SecurityTestData.Grant()).ShouldBe(
            new FileWriteRequest(path, "hi", FileWriteMode.CreateNew, SecurityTestData.Grant()));
    }

    [Fact]
    public void FileWritten_Constructor_WhenBytesWrittenNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileWritten(-1));

        exception.ParamName.ShouldBe("bytesWritten");
    }

    [Fact]
    public void FileWritten_Constructor_WhenValid_RoundTripsBytesWritten()
    {
        var written = new FileWritten(42);

        written.BytesWritten.ShouldBe(42);
    }

    [Fact]
    public void FileAlreadyExists_Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");

        var result = new FileAlreadyExists(path);

        result.Path.ShouldBe(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileWriteDenied_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileWriteDenied(safeMessage!));

    [Fact]
    public void FileWriteDenied_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var denied = new FileWriteDenied("too large");

        denied.SafeMessage.ShouldBe("too large");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileWriteFailed_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileWriteFailed(safeMessage!));

    [Fact]
    public void FileWriteFailed_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var failed = new FileWriteFailed("disk error");

        failed.SafeMessage.ShouldBe("disk error");
    }

    [Fact]
    public void FileWriteResult_Hierarchy_EveryLeafDerivesFromFileWriteResult()
    {
        FileWriteResult written = new FileWritten(1);
        FileWriteResult alreadyExists = new FileAlreadyExists(new FileSystemPath("a.txt"));
        FileWriteResult denied = new FileWriteDenied("no");
        FileWriteResult failed = new FileWriteFailed("no");

        _ = written.ShouldBeOfType<FileWritten>();
        _ = alreadyExists.ShouldBeOfType<FileAlreadyExists>();
        _ = denied.ShouldBeOfType<FileWriteDenied>();
        _ = failed.ShouldBeOfType<FileWriteFailed>();
    }
}
