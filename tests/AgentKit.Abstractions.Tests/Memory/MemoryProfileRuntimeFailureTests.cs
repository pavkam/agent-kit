// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryProfileRuntimeFailure"/> constraints.</summary>
public sealed class MemoryProfileRuntimeFailureTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesKindAndMessage()
    {
        var failure = new MemoryProfileRuntimeFailure(MemoryProfileRuntimeFailureKind.VersionMismatch, "stale");

        failure.Kind.ShouldBe(MemoryProfileRuntimeFailureKind.VersionMismatch);
        failure.SafeMessage.ShouldBe("stale");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryProfileRuntimeFailure((MemoryProfileRuntimeFailureKind) 9, "m")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryProfileRuntimeFailure(MemoryProfileRuntimeFailureKind.UnknownProfile, " ")).ParamName.ShouldBe("safeMessage");
}
