// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

/// <summary>Verifies <see cref="RequiredRunEventSinkDrainResult"/> validation and cleanliness.</summary>
public sealed class RequiredRunEventSinkDrainResultTests
{
    [Fact]
    public void Constructor_WhenAnArrayIsDefault_ThrowsArgumentExceptionNamingIt()
    {
        Should.Throw<ArgumentException>(() => new RequiredRunEventSinkDrainResult(default, [], [])).ParamName.ShouldBe("drained");
        Should.Throw<ArgumentException>(() => new RequiredRunEventSinkDrainResult([], default, [])).ParamName.ShouldBe("timedOut");
        Should.Throw<ArgumentException>(() => new RequiredRunEventSinkDrainResult([], [], default)).ParamName.ShouldBe("failed");
    }

    [Fact]
    public void Empty_WhenRead_IsCleanAndHasNoSinks()
    {
        RequiredRunEventSinkDrainResult.Empty.IsClean.ShouldBeTrue();
        RequiredRunEventSinkDrainResult.Empty.Drained.ShouldBeEmpty();
    }

    [Fact]
    public void IsClean_WhenASinkTimedOutOrFailed_IsFalse()
    {
        new RequiredRunEventSinkDrainResult(["a"], ["b"], []).IsClean.ShouldBeFalse();
        new RequiredRunEventSinkDrainResult(["a"], [], ["c"]).IsClean.ShouldBeFalse();
        new RequiredRunEventSinkDrainResult(["a"], [], []).IsClean.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesEveryBucket()
    {
        var result = new RequiredRunEventSinkDrainResult(["a"], ["b"], ["c"]);

        result.Drained.ShouldBe(["a"]);
        result.TimedOut.ShouldBe(["b"]);
        result.Failed.ShouldBe(["c"]);
    }
}
