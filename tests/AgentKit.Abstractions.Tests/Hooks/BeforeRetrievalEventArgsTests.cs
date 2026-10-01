// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="BeforeRetrievalEventArgs"/> narrowing-only budget mutation and isolation state.</summary>
public sealed class BeforeRetrievalEventArgsTests
{
    private static BeforeRetrievalEventArgs Create(RetrievalBudget? budget = null)
    {
        var owner = MemoryTestData.NewOwner();
        var effective = budget ?? new RetrievalBudget(10, 1000, 200);
        return new BeforeRetrievalEventArgs(
            HookKernelTestData.Dispatch(AgentHookPoints.BeforeRetrieval), MemoryTestData.Query(owner, budget: effective), effective);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_StartsAtTheEffectiveBudgetUnnarrowed()
    {
        var args = Create();

        args.MaximumItems.ShouldBe(10);
        args.MaximumBytes.ShouldBe(1000);
        args.BudgetNarrowed.ShouldBeFalse();
        args.AgentId.ShouldBe(args.Query.Context.AgentId);
        args.SessionId.ShouldBe(args.Query.Context.SessionId);
        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var owner = MemoryTestData.NewOwner();
        var dispatch = HookKernelTestData.Dispatch(AgentHookPoints.BeforeRetrieval);
        var budget = new RetrievalBudget(10, 1000, 200);

        Should.Throw<ArgumentNullException>(() => new BeforeRetrievalEventArgs(null!, MemoryTestData.Query(owner), budget)).ParamName.ShouldBe("dispatch");
        Should.Throw<ArgumentNullException>(() => new BeforeRetrievalEventArgs(dispatch, null!, budget)).ParamName.ShouldBe("query");
        Should.Throw<ArgumentNullException>(() => new BeforeRetrievalEventArgs(dispatch, MemoryTestData.Query(owner), null!)).ParamName.ShouldBe("effectiveBudget");
    }

    [Fact]
    public void Validate_WhenLimitsAreNarrowed_AcceptsThemAndReportsNarrowing()
    {
        var args = Create();
        args.MaximumItems = 1;
        args.MaximumBytes = 1;

        Should.NotThrow(args.Validate);
        args.BudgetNarrowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(11, 1000)]
    [InlineData(0, 1000)]
    [InlineData(-1, 1000)]
    [InlineData(10, 1001)]
    [InlineData(10, 0)]
    public void Validate_WhenALimitIsWidenedOrNotPositive_ThrowsHookValidationException(int items, int bytes)
    {
        var args = Create();
        args.MaximumItems = items;
        args.MaximumBytes = bytes;

        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void RestoreMutableState_WhenAHookNarrowedTheBudget_RestoresTheCapturedLimits()
    {
        var args = Create();
        var captured = args.CaptureMutableState();
        args.MaximumItems = 2;
        args.MaximumBytes = 5;

        args.RestoreMutableState(captured);

        args.MaximumItems.ShouldBe(10);
        args.MaximumBytes.ShouldBe(1000);
        args.RestoreMutableState("not a snapshot");
        args.MaximumItems.ShouldBe(10);
    }
}
