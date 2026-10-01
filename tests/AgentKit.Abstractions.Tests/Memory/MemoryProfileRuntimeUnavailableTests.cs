// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryProfileRuntimeUnavailable"/> constraints.</summary>
public sealed class MemoryProfileRuntimeUnavailableTests
{
    private static readonly MemoryProfileRuntimeFailure _failure = new(MemoryProfileRuntimeFailureKind.UnknownProfile, "unknown");

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var result = new MemoryProfileRuntimeUnavailable(new MemoryProfileKey("p"), new MemoryProfileVersion(2), _failure);

        result.ProfileKey.ShouldBe(new MemoryProfileKey("p"));
        result.ProfileVersion.ShouldBe(new MemoryProfileVersion(2));
        result.Failure.ShouldBe(_failure);
        _ = result.ShouldBeAssignableTo<MemoryProfileRuntimeSelectionResult>();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new MemoryProfileRuntimeUnavailable(default, new MemoryProfileVersion(1), _failure)).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryProfileRuntimeUnavailable(new MemoryProfileKey("p"), default, _failure)).ParamName.ShouldBe("profileVersion");
        Should.Throw<ArgumentNullException>(() => new MemoryProfileRuntimeUnavailable(new MemoryProfileKey("p"), new MemoryProfileVersion(1), null!)).ParamName.ShouldBe("failure");
    }
}
