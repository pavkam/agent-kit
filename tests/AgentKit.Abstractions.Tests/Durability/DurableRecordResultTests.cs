// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Exercises journal write outcomes, including the fencing invariant that a
/// rejection is only meaningful when the presented generation is older.
/// </summary>
public sealed class DurableRecordResultTests
{
    [Fact]
    public void DurableRecorded_Constructor_WhenTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableRecorded(default, DurabilityTestData.Now));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void DurableRecorded_Constructor_WhenTokenIsAllocated_Succeeds()
    {
        var recorded = new DurableRecorded(new FencingToken(3), DurabilityTestData.Now);

        recorded.FencingToken.ShouldBe(new FencingToken(3));
        recorded.RecordedAt.ShouldBe(DurabilityTestData.Now);
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenCurrentTokenIsNewer_Succeeds()
    {
        var fenced = new DurableRecordFenced(new FencingToken(1), new FencingToken(2));

        fenced.PresentedToken.ShouldBe(new FencingToken(1));
        fenced.CurrentToken.ShouldBe(new FencingToken(2));
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenCurrentTokenEqualsPresented_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableRecordFenced(new FencingToken(2), new FencingToken(2)));

        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenCurrentTokenIsOlder_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableRecordFenced(new FencingToken(5), new FencingToken(4)));

        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void DurableRecordFenced_Constructor_WhenPresentedTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableRecordFenced(default, new FencingToken(1)));

        exception.ParamName.ShouldBe("presentedToken");
    }

    [Fact]
    public void DurableRecordFailed_Constructor_WhenMessageIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableRecordFailed("  "));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void DurableRecordFailed_Constructor_WhenCommittedIsOmitted_DurabilityIsUnknown() =>
        new DurableRecordFailed("store unavailable").Committed.ShouldBeNull();

    [Fact]
    public void DurableRecordFailed_Constructor_WhenCommittedIsProven_PreservesIt() =>
        new DurableRecordFailed("partial failure", committed: true).Committed.ShouldBe(true);

    [Fact]
    public void Hierarchy_ContainsOnlyTheThreeDeclaredKinds()
    {
        var kinds = typeof(DurableRecordResult).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(DurableRecordResult)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        kinds.ShouldBe([
            nameof(DurableRecordFailed),
            nameof(DurableRecordFenced),
            nameof(DurableRecorded),
        ]);
    }
}
