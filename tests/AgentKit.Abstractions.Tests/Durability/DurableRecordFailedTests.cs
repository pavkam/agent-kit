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
}
