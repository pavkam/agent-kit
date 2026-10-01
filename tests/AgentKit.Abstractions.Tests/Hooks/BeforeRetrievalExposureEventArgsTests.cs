// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="BeforeRetrievalExposureEventArgs"/> narrowing-only exclusion and isolation state.</summary>
public sealed class BeforeRetrievalExposureEventArgsTests
{
    private static BeforeRetrievalExposureEventArgs Create(int candidates = 3)
    {
        var owner = MemoryTestData.NewOwner();
        var query = MemoryTestData.Query(owner);
        var offered = Enumerable.Range(0, candidates)
            .Select(index => MemoryTestData.Candidate(query.Id, $"candidate {index}", score: 1 - (index * 0.1)))
            .ToImmutableArray();
        return new BeforeRetrievalExposureEventArgs(HookKernelTestData.Dispatch(AgentHookPoints.BeforeRetrievalExposure), query, offered);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesEveryCandidateWithNothingExcluded()
    {
        var args = Create();

        args.Candidates.Length.ShouldBe(3);
        args.ExcludedPositions.ShouldBeEmpty();
        args.Remaining.ShouldBe(args.Candidates);
        args.AgentId.ShouldBe(args.Query.Context.AgentId);
        args.SessionId.ShouldBe(args.Query.Context.SessionId);
        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsTheExactException()
    {
        var owner = MemoryTestData.NewOwner();
        var query = MemoryTestData.Query(owner);
        var dispatch = HookKernelTestData.Dispatch(AgentHookPoints.BeforeRetrievalExposure);

        Should.Throw<ArgumentNullException>(() => new BeforeRetrievalExposureEventArgs(null!, query, [])).ParamName.ShouldBe("dispatch");
        Should.Throw<ArgumentNullException>(() => new BeforeRetrievalExposureEventArgs(dispatch, null!, [])).ParamName.ShouldBe("query");
        Should.Throw<ArgumentException>(() => new BeforeRetrievalExposureEventArgs(dispatch, query, default)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentException>(() => new BeforeRetrievalExposureEventArgs(dispatch, query, [null!])).ParamName.ShouldBe("candidates");
    }

    [Fact]
    public void Remaining_WhenPositionsAreExcluded_DropsThemAndKeepsRankedOrder()
    {
        var args = Create();
        args.ExcludedPositions = [1];

        args.Remaining.ShouldBe([args.Candidates[0], args.Candidates[2]]);
        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void Remaining_WhenEveryPositionIsExcluded_IsEmpty()
    {
        var args = Create();
        args.ExcludedPositions = [0, 1, 2];

        args.Remaining.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void Validate_WhenAPositionIsOutsideTheOfferedSet_ThrowsHookValidationException(int position)
    {
        var args = Create();
        args.ExcludedPositions = [position];

        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void Validate_WhenAPositionRepeatsOrTheArrayIsDefault_ThrowsHookValidationException()
    {
        var repeated = Create();
        repeated.ExcludedPositions = [0, 0];
        var replaced = Create();
        replaced.ExcludedPositions = default;

        _ = Should.Throw<HookValidationException>(repeated.Validate);
        _ = Should.Throw<HookValidationException>(replaced.Validate);
        replaced.Remaining.Length.ShouldBe(3);
    }

    [Fact]
    public void RestoreMutableState_WhenAHookExcludedCandidates_RestoresTheCapturedExclusions()
    {
        var args = Create();
        var captured = args.CaptureMutableState();
        args.ExcludedPositions = [0, 1];

        args.RestoreMutableState(captured);

        args.ExcludedPositions.ShouldBeEmpty();
        args.RestoreMutableState("not a snapshot");
        args.ExcludedPositions.ShouldBeEmpty();
    }
}
