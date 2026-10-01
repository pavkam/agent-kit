// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="BeforeMemoryWriteEventArgs"/> identity, veto, short-circuit, and isolation state.</summary>
public sealed class BeforeMemoryWriteEventArgsTests
{
    private static (MemoryTestOwner Owner, DurableMemoryRecord Record) Record()
    {
        var owner = MemoryTestData.NewOwner();
        return (owner, MemoryTestData.Record(owner));
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesTheRecordAndStartsUnvetoed()
    {
        var (owner, record) = Record();

        var args = new BeforeMemoryWriteEventArgs(HookKernelTestData.Dispatch(AgentHookPoints.BeforeMemoryWrite), owner.Context, record);

        args.Record.ShouldBeSameAs(record);
        args.Context.ShouldBeSameAs(owner.Context);
        args.AgentId.ShouldBe(owner.Context.AgentId);
        args.SessionId.ShouldBe(owner.Context.SessionId);
        args.Veto.ShouldBeNull();
        args.IsShortCircuited.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var (owner, record) = Record();
        var dispatch = HookKernelTestData.Dispatch(AgentHookPoints.BeforeMemoryWrite);

        Should.Throw<ArgumentNullException>(() => new BeforeMemoryWriteEventArgs(null!, owner.Context, record)).ParamName.ShouldBe("dispatch");
        Should.Throw<ArgumentNullException>(() => new BeforeMemoryWriteEventArgs(dispatch, null!, record)).ParamName.ShouldBe("context");
        Should.Throw<ArgumentNullException>(() => new BeforeMemoryWriteEventArgs(dispatch, owner.Context, null!)).ParamName.ShouldBe("record");
    }

    [Fact]
    public void Veto_WhenSet_ShortCircuitsAndRestoresToTheCapturedState()
    {
        var (owner, record) = Record();
        var args = new BeforeMemoryWriteEventArgs(HookKernelTestData.Dispatch(AgentHookPoints.BeforeMemoryWrite), owner.Context, record);
        var captured = args.CaptureMutableState();
        args.Veto = new MemoryHookVeto("hold", "A hold applies.");

        args.IsShortCircuited.ShouldBeTrue();
        args.RestoreMutableState(captured);

        args.Veto.ShouldBeNull();
    }
}
