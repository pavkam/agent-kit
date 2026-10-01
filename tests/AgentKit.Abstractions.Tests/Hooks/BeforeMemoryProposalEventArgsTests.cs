// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="BeforeMemoryProposalEventArgs"/> identity, veto, short-circuit, and isolation state.</summary>
public sealed class BeforeMemoryProposalEventArgsTests
{
    private static BeforeMemoryProposalEventArgs Create(MemoryProposal? proposal = null) =>
        new(HookKernelTestData.Dispatch(AgentHookPoints.BeforeMemoryProposal), proposal ?? MemoryTestData.Proposal(MemoryTestData.NewOwner()));

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesTheProposalIdentitiesAndStartsUnvetoed()
    {
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner());

        var args = Create(proposal);

        args.Proposal.ShouldBeSameAs(proposal);
        args.AgentId.ShouldBe(proposal.Context.AgentId);
        args.SessionId.ShouldBe(proposal.Context.SessionId);
        args.Point.ShouldBe(AgentHookPoints.BeforeMemoryProposal);
        args.Veto.ShouldBeNull();
        args.IsShortCircuited.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var dispatch = HookKernelTestData.Dispatch(AgentHookPoints.BeforeMemoryProposal);

        Should.Throw<ArgumentNullException>(() => new BeforeMemoryProposalEventArgs(null!, MemoryTestData.Proposal(MemoryTestData.NewOwner()))).ParamName.ShouldBe("dispatch");
        Should.Throw<ArgumentNullException>(() => new BeforeMemoryProposalEventArgs(dispatch, null!)).ParamName.ShouldBe("proposal");
    }

    [Fact]
    public void Veto_WhenSet_ShortCircuitsAndRestoresToTheCapturedState()
    {
        var args = Create();
        var captured = args.CaptureMutableState();
        args.Veto = new MemoryHookVeto("hold", "A hold applies.");

        args.IsShortCircuited.ShouldBeTrue();
        args.RestoreMutableState(captured);

        args.Veto.ShouldBeNull();
        args.IsShortCircuited.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WhenVetoIsSet_DoesNotThrow()
    {
        var args = Create();
        args.Veto = new MemoryHookVeto("hold", "A hold applies.");

        Should.NotThrow(args.Validate);
    }
}
