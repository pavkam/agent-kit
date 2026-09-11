// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableRecorded behavior and contracts.</summary>
public sealed class DurableRecordedTests
{
    [Fact]
    public void DurableRecorded_Constructor_WhenTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableRecorded(default, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void DurableRecorded_Constructor_WhenTokenIsAllocated_Succeeds()
    {
        var recorded = new DurableRecorded(new FencingToken(3), DurabilityTestData.Now);
        recorded.FencingToken.ShouldBe(new FencingToken(3));
        recorded.RecordedAt.ShouldBe(DurabilityTestData.Now);
    }
}
