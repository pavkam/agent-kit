// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableRecordFailed behavior and contracts.</summary>
public sealed class DurableRecordFailedTests
{
    [Fact]
    public void DurableRecordFailed_Constructor_WhenMessageIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableRecordFailed("  "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void DurableRecordFailed_Constructor_WhenCommittedIsOmitted_DurabilityIsUnknown() => new DurableRecordFailed("store unavailable").Committed.ShouldBeNull();
    [Fact]
    public void DurableRecordFailed_Constructor_WhenCommittedIsProven_PreservesIt() => new DurableRecordFailed("partial failure", committed: true).Committed.ShouldBe(true);

    [Fact]
    public void DurableRecordFailed_Constructor_WhenArgumentsAreValid_RoundTripsSafeMessage()
    {
        var failed = new DurableRecordFailed("store unavailable");
        failed.SafeMessage.ShouldBe("store unavailable");
    }

    [Fact]
    public void With_WhenSafeMessageIsWhitespace_ThrowsArgumentException()
    {
        var failed = new DurableRecordFailed("store unavailable");
        Should.Throw<ArgumentException>(() => _ = failed with { SafeMessage = " " }).ParamName.ShouldBe("SafeMessage");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DurableRecordFailed("store unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenSafeMessageIsValid_UpdatesSafeMessage()
    {
        var original = new DurableRecordFailed("store unavailable");
        var updated = original with { SafeMessage = "other message" };
        updated.SafeMessage.ShouldBe("other message");
    }
}
