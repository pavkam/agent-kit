// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryStoreFailure"/> constraints.</summary>
public sealed class MemoryStoreFailureTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesKindAndMessage()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.VersionConflict, "stale");

        failure.Kind.ShouldBe(MemoryStoreFailureKind.VersionConflict);
        failure.SafeMessage.ShouldBe("stale");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryStoreFailure((MemoryStoreFailureKind) 99, "m")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenMessageIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryStoreFailure(MemoryStoreFailureKind.Denied, null!)).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryStoreFailure(MemoryStoreFailureKind.Denied, " ")).ParamName.ShouldBe("safeMessage");
}
